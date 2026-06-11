using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;
using MMS.Infrastructure.Persistence.Services;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/walk-in")]
[Authorize]
public class WalkInController(
    AppDbContext db,
    WalkInService walkInService,
    IRealtimeService realtime,
    ActivityTimelineService timeline,
    NotificationService notifications) : ControllerBase
{
    // ส่งแจ้งเตือน LINE ตอนจัดคิวให้หมอนวด: หาหมอนวด + แจ้งหมอนวด + แจ้งผู้จัดการ/เจ้าของ
    private async Task NotifyAssignmentAsync(Guid walkInId, Guid therapistId)
    {
        var tenantId = User.GetTenantId();
        var w = await db.WalkIns
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(i => i.Service)
            .FirstOrDefaultAsync(x => x.Id == walkInId);
        if (w == null) return;

        var therapist = await db.Therapists.FirstOrDefaultAsync(t => t.Id == therapistId);
        var svcNames = string.Join(", ", w.Items.Select(i => i.Service.Name));
        var custName = w.Customer?.DisplayName ?? "ลูกค้า";
        var thName = therapist?.DisplayName ?? "หมอนวด";
        var startLocal = (w.StartTime ?? DateTime.UtcNow).AddHours(7).ToString("HH:mm");

        // 1) แจ้งหมอนวดที่ได้รับคิว (ผ่าน User ที่ผูก LINE)
        if (therapist?.UserId != null)
        {
            var thUser = await db.Users.FirstOrDefaultAsync(u => u.Id == therapist.UserId);
            if (!string.IsNullOrEmpty(thUser?.LineUserId))
                await notifications.QueueLineToTherapistAsync(thUser.LineUserId,
                    $"🔔 คุณได้รับคิวใหม่\nคิว {w.QueueNo} · {custName}\nบริการ: {svcNames}\nเริ่ม ~{startLocal} น.",
                    "QueueAssigned", tenantId);
        }

        // 2) แจ้งผู้จัดการ/เจ้าของร้านที่ผูก LINE ไว้
        var managerLineIds = await db.UserRoles
            .Where(ur => ur.User.TenantId == tenantId && ur.User.DeletedAt == null
                && ur.User.LineUserId != null
                && (ur.Role.Name == "Owner" || ur.Role.Name == "Manager"))
            .Select(ur => ur.User.LineUserId!).Distinct().ToListAsync();
        foreach (var lid in managerLineIds)
            await notifications.QueueLineToTherapistAsync(lid,
                $"📋 จัดคิวแล้ว\nคิว {w.QueueNo} · {custName}\nบริการ: {svcNames}\nมอบหมาย: {thName} · เริ่ม ~{startLocal} น.",
                "QueueAssignedManager", tenantId);
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.WalkInView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] WalkInStatus? status,
        [FromQuery] DateOnly? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var query = db.WalkIns
            .Where(w => w.TenantId == tenantId && w.BranchId == branchId && w.DeletedAt == null);

        if (status.HasValue) query = query.Where(w => w.Status == status);
        if (date.HasValue)
        {
            var from = date.Value.ToDateTime(TimeOnly.MinValue);
            var to = date.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(w => w.ArrivalTime >= from && w.ArrivalTime <= to);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(w => w.ArrivalTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(w => new
            {
                w.Id,
                w.QueueNo,
                w.Status,
                w.ArrivalTime,
                w.StartTime,
                w.EndTime,
                w.EstimatedWaitMins,
                w.TotalAmount,
                Customer = new { w.Customer.Id, w.Customer.DisplayName, w.Customer.Phone },
                ItemCount = w.Items.Count
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.WalkInView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var walkIn = await db.WalkIns
            .Include(w => w.Customer)
            .Include(w => w.Items).ThenInclude(i => i.Service)
            .Include(w => w.Items).ThenInclude(i => i.Therapist)
            .Include(w => w.Items).ThenInclude(i => i.Room)
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId && w.DeletedAt == null);

        if (walkIn == null) return NotFound(new { message = "WalkIn not found" });

        return Ok(new
        {
            walkIn.Id,
            walkIn.QueueNo,
            walkIn.Status,
            walkIn.ArrivalTime,
            walkIn.StartTime,
            walkIn.EndTime,
            walkIn.EstimatedWaitMins,
            walkIn.TotalAmount,
            walkIn.Notes,
            Customer = new { walkIn.Customer.Id, walkIn.Customer.DisplayName, walkIn.Customer.Phone },
            Items = walkIn.Items.OrderBy(i => i.SortOrder).Select(i => new
            {
                i.Id,
                i.SortOrder,
                i.StartTime,
                i.EndTime,
                i.DurationMins,
                i.Price,
                i.CommissionAmount,
                Service = new { i.Service.Id, i.Service.Name },
                Therapist = i.Therapist == null ? null
                    : new { i.Therapist.Id, i.Therapist.DisplayName, i.Therapist.Code },
                Room = i.Room == null ? null : new { i.Room.Id, i.Room.Name }
            })
        });
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Create([FromBody] CreateWalkInRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "WalkIn must have at least 1 item" });

        var result = await walkInService.CreateWalkInAsync(tenantId, branchId, req);
        if (!result.Success) return BadRequest(new { message = result.Error });

        // 🔔 Realtime — มีลูกค้าใหม่เข้า Queue
        var customer = await db.Customers.FindAsync(req.CustomerId);
        await realtime.NotifyQueueUpdatedAsync(
            branchId, result.WalkInId!.Value, result.QueueNo!,
            customer?.DisplayName ?? "", "Waiting", result.EstimatedWaitMins);

        await timeline.LogAsync("walkin_created", "WalkIn", result.WalkInId!.Value,
            $"เปิดคิว {result.QueueNo} · {customer?.DisplayName}", result.QueueNo);

        return Ok(new
        {
            message = "Walk-in created",
            walkInId = result.WalkInId,
            queueNo = result.QueueNo,
            estimatedWaitMins = result.EstimatedWaitMins
        });
    }

    [HttpPatch("{id:guid}/assign")]
    [RequirePermission(PermissionCodes.WalkInAssign)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await walkInService.AssignTherapistAsync(id, tenantId, req.TherapistId, req.RoomId);
        if (!ok) return BadRequest(new { message = err });

        // 🔔 Realtime — therapist status เปลี่ยนเป็น Occupied
        if (req.TherapistId.HasValue)
        {
            var therapist = await db.Therapists.FindAsync(req.TherapistId.Value);
            if (therapist != null)
                await realtime.NotifyTherapistStatusChangedAsync(
                    branchId, req.TherapistId.Value, therapist.DisplayName, "Occupied", "Available");

            // 🔔 LINE — แจ้งหมอนวดที่ได้คิว + ผู้จัดการ/เจ้าของ
            try { await NotifyAssignmentAsync(id, req.TherapistId.Value); } catch { }
        }

        return Ok(new { message = "Therapist assigned" });
    }

    [HttpPatch("{id:guid}/start")]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Start(Guid id)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await walkInService.StartServiceAsync(id, tenantId);
        if (!ok) return BadRequest(new { message = err });

        // 🔔 Realtime — Queue เปลี่ยนเป็น InService
        var walkIn = await db.WalkIns.Include(w => w.Customer)
            .FirstOrDefaultAsync(w => w.Id == id);
        await realtime.NotifyQueueUpdatedAsync(
            branchId, id, walkIn?.QueueNo ?? "", walkIn?.Customer.DisplayName ?? "", "InService");

        return Ok(new { message = "Service started" });
    }

    [HttpPatch("{id:guid}/complete")]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Complete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await walkInService.CompleteAsync(id, tenantId);
        if (!ok) return BadRequest(new { message = err });

        var walkIn = await db.WalkIns
            .Include(w => w.Customer)
            .Include(w => w.Items).ThenInclude(i => i.Therapist)
            .FirstOrDefaultAsync(w => w.Id == id);

        // 🔔 Realtime — Queue เสร็จสิ้น
        await realtime.NotifyQueueUpdatedAsync(
            branchId, id, walkIn?.QueueNo ?? "", walkIn?.Customer.DisplayName ?? "", "Completed");

        await timeline.LogAsync("walkin_completed", "WalkIn", id,
            $"เสร็จบริการ {walkIn?.QueueNo} · {walkIn?.Customer.DisplayName}", walkIn?.QueueNo);

        // 🔔 Realtime — Therapist กลับเป็น Available
        if (walkIn != null)
            foreach (var item in walkIn.Items.Where(i => i.Therapist != null))
                await realtime.NotifyTherapistStatusChangedAsync(
                    branchId, item.TherapistId!.Value,
                    item.Therapist!.DisplayName, "Available", "Occupied");

        return Ok(new { message = "Service completed" });
    }

    [HttpPatch("{id:guid}/cancel")]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelWalkInRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await walkInService.CancelAsync(id, tenantId, req.Reason);
        if (!ok) return BadRequest(new { message = err });

        // 🔔 Realtime
        var walkIn = await db.WalkIns.Include(w => w.Customer)
            .FirstOrDefaultAsync(w => w.Id == id);
        await realtime.NotifyQueueUpdatedAsync(
            branchId, id, walkIn?.QueueNo ?? "", walkIn?.Customer.DisplayName ?? "", "Cancelled");

        return Ok(new { message = "Walk-in cancelled" });
    }

    /// <summary>
    /// GET /api/walk-in/available-therapists?serviceIds=xxx&serviceIds=yyy
    /// คืน therapist ที่ว่างแยกตามแต่ละ service
    /// </summary>
    [HttpGet("available-therapists")]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> GetAvailableTherapists([FromQuery] List<Guid> serviceIds)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var therapists = await db.Therapists
            .Include(t => t.TherapistServices)
            .Where(t => t.TenantId == tenantId
                && t.BranchId == branchId
                && t.IsActive
                && t.CurrentStatus == TherapistStatus.Available
                && t.DeletedAt == null)
            .ToListAsync();

        // คืนเป็น map: serviceId → list of therapist ที่ทำได้
        var result = serviceIds.ToDictionary(
            sid => sid,
            sid => therapists
                .Where(t => t.TherapistServices
                    .Any(ts => ts.ServiceId == sid && ts.IsActive))
                .Select(t => new
                {
                    t.Id,
                    t.DisplayName,
                    t.Code,
                    t.AvatarUrl,
                    t.SkillLevel
                })
                .ToList()
        );

        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QueueController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.QueueView)]
    public async Task<IActionResult> GetTodayQueue()
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from = today.ToDateTime(TimeOnly.MinValue);
        var to = today.ToDateTime(TimeOnly.MaxValue);

        var waiting = await db.WalkIns
            .Where(w => w.TenantId == tenantId && w.BranchId == branchId
                && w.Status == WalkInStatus.Waiting
                && w.ArrivalTime >= from && w.ArrivalTime <= to && w.DeletedAt == null)
            .OrderBy(w => w.ArrivalTime)
            .Select(w => new
            {
                w.Id,
                w.QueueNo,
                w.ArrivalTime,
                w.EstimatedWaitMins,
                Customer = new { w.Customer.DisplayName, w.Customer.Phone },
                Services = w.Items.Select(i => i.Service.Name)
            })
            .ToListAsync();

        var inService = await db.WalkIns
            .Where(w => w.TenantId == tenantId && w.BranchId == branchId
                && w.Status == WalkInStatus.InService && w.DeletedAt == null)
            .Select(w => new
            {
                w.Id,
                w.QueueNo,
                w.StartTime,
                w.EndTime,
                Customer = new { w.Customer.DisplayName },
                Therapists = w.Items.Where(i => i.Therapist != null)
                    .Select(i => new { i.Therapist!.DisplayName, i.Therapist.Code })
            })
            .ToListAsync();

        var therapists = await db.Therapists
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId
                && t.IsActive && t.DeletedAt == null)
            .Select(t => new { t.Id, t.DisplayName, t.Code, t.AvatarUrl, t.CurrentStatus })
            .ToListAsync();

        return Ok(new
        {
            date = today,
            summary = new
            {
                waitingCount = waiting.Count,
                inServiceCount = inService.Count,
                availableTherapists = therapists.Count(t => t.CurrentStatus == TherapistStatus.Available),
                totalTherapists = therapists.Count
            },
            waiting,
            inService,
            therapists
        });
    }

    [HttpPatch("reorder")]
    [RequirePermission(PermissionCodes.QueueManage)]
    public async Task<IActionResult> Reorder([FromBody] List<QueueOrderItem> order)
    {
        var tenantId = User.GetTenantId();
        foreach (var item in order)
        {
            var walkIn = await db.WalkIns
                .FirstOrDefaultAsync(w => w.Id == item.WalkInId
                    && w.TenantId == tenantId
                    && w.Status == WalkInStatus.Waiting
                    && w.DeletedAt == null);

            if (walkIn != null)
                walkIn.EstimatedWaitMins = item.EstimatedWaitMins;
        }
        await db.SaveChangesAsync();
        return Ok(new { message = "Queue reordered" });
    }
}

public record AssignRequest(Guid? TherapistId, Guid? RoomId);
public record CancelWalkInRequest(string? Reason);
public record QueueOrderItem(Guid WalkInId, int EstimatedWaitMins);
