using Microsoft.EntityFrameworkCore;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;

namespace MMS.Infrastructure.Persistence.Services;

public class WalkInService(AppDbContext db, IRealtimeService realtime)
{
    // ──────────────────────────────────────────────
    // CREATE WALK-IN
    // ──────────────────────────────────────────────

    public async Task<WalkInResult> CreateWalkInAsync(
        Guid tenantId, Guid branchId, CreateWalkInRequest req)
    {
        // 1. ตรวจ Customer
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == req.CustomerId
                && c.TenantId == tenantId && c.DeletedAt == null);
        if (customer == null)
            return WalkInResult.Fail("Customer not found");

        // 2. ตรวจ Services
        var items = new List<WalkInItemData>();
        foreach (var item in req.Items.OrderBy(i => i.SortOrder))
        {
            var service = await db.Services
                .FirstOrDefaultAsync(s => s.Id == item.ServiceId
                    && s.TenantId == tenantId && s.IsActive && s.DeletedAt == null);
            if (service == null)
                return WalkInResult.Fail($"Service {item.ServiceId} not found");

            decimal? commission = null;
            if (item.TherapistId.HasValue)
            {
                if (service.CommissionFixed.HasValue)
                    commission = service.CommissionFixed;
                else if (service.CommissionRate.HasValue)
                    commission = service.Price * service.CommissionRate.Value / 100;
            }

            items.Add(new WalkInItemData(
                service, item.TherapistId, item.RoomId,
                service.DurationMins, service.Price, commission, item.SortOrder));
        }

        // 3. คำนวณ estimated wait
        var estimatedWait = await CalculateEstimatedWaitAsync(tenantId, branchId);

        // 4. สร้าง QueueNo
        var queueNo = await GenerateQueueNoAsync(tenantId, branchId);
        var totalAmount = items.Sum(i => i.Price);

        var walkIn = new WalkIn
        {
            TenantId = tenantId,
            BranchId = branchId,
            QueueNo = queueNo,
            CustomerId = req.CustomerId,
            ArrivalTime = DateTime.UtcNow.AddHours(7), // Thai time
            EstimatedWaitMins = estimatedWait,
            TotalAmount = totalAmount,
            Notes = req.Notes,
            Status = WalkInStatus.Waiting
        };

        db.WalkIns.Add(walkIn);
        await db.SaveChangesAsync();

        // 5. สร้าง WalkInItems
        foreach (var item in items)
        {
            db.WalkInItems.Add(new WalkInItem
            {
                TenantId = tenantId,
                WalkInId = walkIn.Id,
                ServiceId = item.Service.Id,
                TherapistId = item.TherapistId,
                RoomId = item.RoomId,
                DurationMins = item.DurationMins,
                Price = item.Price,
                CommissionAmount = item.Commission,
                SortOrder = item.SortOrder
            });
        }

        await db.SaveChangesAsync();

        // 🔴 Broadcast คิวใหม่
        await realtime.NotifyQueueUpdatedAsync(
            branchId, walkIn.Id,
            queueNo,
            customer.DisplayName,
            WalkInStatus.Waiting.ToString(),
            estimatedWait);

        return WalkInResult.Ok(walkIn.Id, queueNo, estimatedWait);
    }

    // ──────────────────────────────────────────────
    // ASSIGN THERAPIST (เมื่อมีช่างว่าง)
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AssignTherapistAsync(
     Guid walkInId, Guid tenantId, Guid? manualTherapistId = null, Guid? roomId = null)
    {
        var walkIn = await db.WalkIns
            .Include(w => w.Items)
                .ThenInclude(i => i.Service)
            .FirstOrDefaultAsync(w => w.Id == walkInId
                && w.TenantId == tenantId && w.DeletedAt == null);

        if (walkIn == null) return (false, "WalkIn not found");
        if (walkIn.Status != WalkInStatus.Waiting)
            return (false, $"Cannot assign to walk-in with status {walkIn.Status}");

        // ดึง therapist ทั้งหมดที่ active + available พร้อม TherapistServices
        var availableTherapists = await db.Therapists
            .Include(t => t.TherapistServices)
            .Where(t => t.TenantId == tenantId
                && t.BranchId == walkIn.BranchId
                && t.IsActive
                && t.CurrentStatus == TherapistStatus.Available
                && t.DeletedAt == null)
            .ToListAsync();

        // track therapist ที่ถูก assign ใน session นี้ (ป้องกัน assign ซ้ำในคิวเดียวกัน)
        var assignedInThisSession = new HashSet<Guid>();

        foreach (var item in walkIn.Items.OrderBy(i => i.SortOrder))
        {
            // ถ้า item นี้มีหมอนวดแล้วข้ามไป
            if (item.TherapistId != null) continue;

            Therapist? therapist;

            if (manualTherapistId.HasValue && walkIn.Items.Count == 1)
            {
                // Manual assign — มีแค่ 1 บริการ
                therapist = availableTherapists
                    .FirstOrDefault(t => t.Id == manualTherapistId.Value
                        && t.TherapistServices.Any(ts => ts.ServiceId == item.ServiceId && ts.IsActive));

                if (therapist == null)
                    return (false, "หมอนวดที่เลือกไม่ว่าง หรือไม่สามารถให้บริการนี้ได้");
            }
            else
            {
                // Auto assign — หาหมอนวดที่ทำบริการนี้ได้ และยังไม่ถูก assign ในคิวนี้
                therapist = availableTherapists
                    .FirstOrDefault(t =>
                        !assignedInThisSession.Contains(t.Id) &&
                        t.TherapistServices.Any(ts => ts.ServiceId == item.ServiceId && ts.IsActive));

                if (therapist == null)
                    return (false, $"ไม่มีหมอนวดที่ว่างสำหรับบริการ '{item.Service.Name}' ในขณะนี้");
            }

            // Assign
            item.TherapistId = therapist.Id;
            assignedInThisSession.Add(therapist.Id);

            // คำนวณ commission
            if (item.Service.CommissionFixed.HasValue)
                item.CommissionAmount = item.Service.CommissionFixed;
            else if (item.Service.CommissionRate.HasValue)
                item.CommissionAmount = item.Price * item.Service.CommissionRate.Value / 100;

            // เปลี่ยน status therapist เป็น Occupied
            therapist.CurrentStatus = TherapistStatus.Occupied;

            if (roomId.HasValue && item.RoomId == null)
                item.RoomId = roomId;
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    // ──────────────────────────────────────────────
    // START SERVICE
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> StartServiceAsync(
        Guid walkInId, Guid tenantId)
    {
        var walkIn = await db.WalkIns
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == walkInId
                && w.TenantId == tenantId && w.DeletedAt == null);

        if (walkIn == null) return (false, "WalkIn not found");
        if (walkIn.Status != WalkInStatus.Waiting)
            return (false, $"Cannot start walk-in with status {walkIn.Status}");

        var now = DateTime.UtcNow.AddHours(7);
        walkIn.Status = WalkInStatus.InService;
        walkIn.StartTime = now;

        // set start/end time ให้แต่ละ item
        var currentTime = now;
        foreach (var item in walkIn.Items.OrderBy(i => i.SortOrder))
        {
            item.StartTime = currentTime;
            item.EndTime = currentTime.AddMinutes(item.DurationMins);
            currentTime = item.EndTime.Value;
        }

        walkIn.EndTime = currentTime; // คาดว่าจะจบเมื่อไหร่

        await db.SaveChangesAsync();

        // 🔴 Broadcast คิวเริ่มบริการ
        await realtime.NotifyQueueUpdatedAsync(
            walkIn.BranchId, walkIn.Id,
            walkIn.QueueNo,
            "", // ไม่ต้องส่งชื่อ ฝั่ง client รู้จากก่อนหน้าแล้ว
            WalkInStatus.InService.ToString());

        return (true, null);
    }

    // ──────────────────────────────────────────────
    // COMPLETE
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> CompleteAsync(
        Guid walkInId, Guid tenantId)
    {
        var walkIn = await db.WalkIns
            .Include(w => w.Items)
                .ThenInclude(i => i.Therapist)
            .FirstOrDefaultAsync(w => w.Id == walkInId
                && w.TenantId == tenantId && w.DeletedAt == null);

        if (walkIn == null) return (false, "WalkIn not found");
        if (walkIn.Status != WalkInStatus.InService)
            return (false, $"Cannot complete walk-in with status {walkIn.Status}");

        var now = DateTime.UtcNow.AddHours(7);
        walkIn.Status = WalkInStatus.Completed;
        walkIn.EndTime = now;

        // เปลี่ยน therapist status กลับเป็น Available
        foreach (var item in walkIn.Items.Where(i => i.Therapist != null))
        {
            item.Therapist!.CurrentStatus = TherapistStatus.Available;
        }

        // อัปเดต customer stats
        var customer = await db.Customers.FindAsync(walkIn.CustomerId);
        if (customer != null)
        {
            customer.TotalVisits = customer.TotalVisits + 1;
            customer.TotalSpent = customer.TotalSpent + (walkIn.TotalAmount ?? 0);
            customer.LoyaltyPoints += (int)((walkIn.TotalAmount ?? 0) / 100);  // 1 แต้ม / 100฿
            customer.LastVisitAt = now;
        }

        await db.SaveChangesAsync();

        // 🔴 Broadcast คิวเสร็จ + therapist กลับมา Available
        await realtime.NotifyQueueUpdatedAsync(
            walkIn.BranchId, walkIn.Id,
            walkIn.QueueNo,
            "",
            WalkInStatus.Completed.ToString());

        foreach (var item in walkIn.Items.Where(i => i.Therapist != null))
        {
            await realtime.NotifyTherapistStatusChangedAsync(
                walkIn.BranchId,
                item.Therapist!.Id,
                item.Therapist.DisplayName,
                TherapistStatus.Available.ToString(),
                TherapistStatus.Occupied.ToString());
        }

        return (true, null);
    }

    // ──────────────────────────────────────────────
    // CANCEL
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> CancelAsync(
        Guid walkInId, Guid tenantId, string? reason)
    {
        var walkIn = await db.WalkIns
            .Include(w => w.Items)
                .ThenInclude(i => i.Therapist)
            .FirstOrDefaultAsync(w => w.Id == walkInId
                && w.TenantId == tenantId && w.DeletedAt == null);

        if (walkIn == null) return (false, "WalkIn not found");
        if (walkIn.Status == WalkInStatus.Completed)
            return (false, "Cannot cancel completed walk-in");

        walkIn.Status = WalkInStatus.Cancelled;
        walkIn.Notes = string.IsNullOrWhiteSpace(reason)
            ? walkIn.Notes
            : $"{walkIn.Notes} [Cancelled: {reason}]";

        // คืน therapist status
        foreach (var item in walkIn.Items.Where(i => i.Therapist != null))
            item.Therapist!.CurrentStatus = TherapistStatus.Available;

        await db.SaveChangesAsync();
        return (true, null);
    }

    // ──────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────

    private async Task<int> CalculateEstimatedWaitAsync(Guid tenantId, Guid branchId)
    {
        // นับจำนวน walk-in ที่รออยู่ × average service duration
        var waitingCount = await db.WalkIns
            .CountAsync(w => w.TenantId == tenantId
                && w.BranchId == branchId
                && w.Status == WalkInStatus.Waiting
                && w.DeletedAt == null);

        var avgDuration = await db.Services
            .Where(s => s.TenantId == tenantId && s.IsActive && s.DeletedAt == null)
            .AverageAsync(s => (double?)s.DurationMins) ?? 60;

        // therapist ที่ว่างอยู่
        var availableTherapists = await db.Therapists
            .CountAsync(t => t.TenantId == tenantId
                && t.BranchId == branchId
                && t.IsActive
                && t.CurrentStatus == TherapistStatus.Available
                && t.DeletedAt == null);

        if (availableTherapists == 0) availableTherapists = 1;

        return (int)Math.Ceiling(waitingCount * avgDuration / availableTherapists);
    }

    private async Task<string> GenerateQueueNoAsync(Guid tenantId, Guid branchId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var todayEnd = today.ToDateTime(TimeOnly.MaxValue);

        var count = await db.WalkIns
            .CountAsync(w => w.TenantId == tenantId
                && w.BranchId == branchId
                && w.ArrivalTime >= todayStart
                && w.ArrivalTime <= todayEnd);

        return $"Q{(count + 1):D3}";
    }
}

// DTOs
public record CreateWalkInRequest(
    Guid CustomerId,
    List<WalkInItemRequest> Items,
    string? Notes = null);

public record WalkInItemRequest(
    Guid ServiceId,
    Guid? TherapistId,
    Guid? RoomId,
    int SortOrder = 0);

public record WalkInItemData(
    Service Service,
    Guid? TherapistId,
    Guid? RoomId,
    int DurationMins,
    decimal Price,
    decimal? Commission,
    int SortOrder);

public record WalkInResult(
    bool Success, Guid? WalkInId, string? QueueNo,
    int? EstimatedWaitMins, string? Error)
{
    public static WalkInResult Ok(Guid id, string queueNo, int waitMins)
        => new(true, id, queueNo, waitMins, null);
    public static WalkInResult Fail(string error)
        => new(false, null, null, null, error);
}
