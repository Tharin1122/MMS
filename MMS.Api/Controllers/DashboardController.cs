using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Enums;
using MMS.Domain.Helper;
using MMS.Infrastructure.Persistence;
using MMS.Infrastructure.Persistence.Services;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(
    AppDbContext db,
    IRealtimeService realtime,
    MMS.Infrastructure.Persistence.Auth.LineOtpService lineNotify) : ControllerBase
{
    // ส่ง LINE ตรงหาผู้จัดการ/เจ้าของ + หมอนวด (ใช้ตอนเลื่อนเวลาคิว)
    private async Task NotifyRescheduleAsync(Guid tenantId, string custName, string thName, Guid? therapistUserId, string newTime, string queueNo)
    {
        var lineIds = await db.UserRoles
            .Where(ur => ur.User.TenantId == tenantId && ur.User.DeletedAt == null && ur.User.LineUserId != null
                && (ur.Role.Name == "Owner" || ur.Role.Name == "Manager"))
            .Select(ur => ur.User.LineUserId!).ToListAsync();
        if (therapistUserId != null)
        {
            var thLine = await db.Users.Where(u => u.Id == therapistUserId && u.LineUserId != null)
                .Select(u => u.LineUserId!).FirstOrDefaultAsync();
            if (thLine != null) lineIds.Add(thLine);
        }
        var msg = $"⏰ เปลี่ยนเวลาคิว\n{queueNo} · {custName}\nหมอนวด: {thName}\nเวลาใหม่: {newTime} น.";
        foreach (var id in lineIds.Distinct())
        { try { await lineNotify.SendTextAsync(id, msg); } catch { } }
    }

    /// <summary>
    /// GET /api/dashboard
    /// ภาพรวมทั้งหมดของวันนี้สำหรับ Staff หน้าจอหลัก
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.DashboardView)]
    public async Task<IActionResult> GetSnapshot()
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var today = ThaiTime.Today;
        var todayStartUtc = today.ToDateTime(TimeOnly.MinValue).AddHours(-7); // 00:00 Thai = 17:00 UTC เมื่อวาน
        var todayEndUtc = today.ToDateTime(TimeOnly.MaxValue).AddHours(-7); // 23:59 Thai = 16:59 UTC วันนี้

        // ── Therapists ──────────────────────────────
        var therapists = await db.Therapists
            .Where(t => t.TenantId == tenantId
                && t.BranchId == branchId
                && t.IsActive
                && t.DeletedAt == null)
            .Select(t => new
            {
                t.Id,
                t.DisplayName,
                t.Code,
                t.AvatarUrl,
                t.CurrentStatus
            })
            .ToListAsync();

        var therapistSummary = new
        {
            total = therapists.Count,
            available = therapists.Count(t => t.CurrentStatus == TherapistStatus.Available),
            occupied = therapists.Count(t => t.CurrentStatus == TherapistStatus.Occupied),
            onBreak = therapists.Count(t => t.CurrentStatus == TherapistStatus.Break),
            onLeave = therapists.Count(t => t.CurrentStatus == TherapistStatus.Leave),
            offline = therapists.Count(t => t.CurrentStatus == TherapistStatus.Offline),
            list = therapists
        };

        // ── Rooms ───────────────────────────────────
        var rooms = await db.Rooms
            .Where(r => r.TenantId == tenantId
                && r.BranchId == branchId
                && r.IsActive
                && r.DeletedAt == null)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.RoomType,
                r.CurrentStatus,
                r.CleaningBufferMins
            })
            .ToListAsync();

        var roomSummary = new
        {
            total = rooms.Count,
            available = rooms.Count(r => r.CurrentStatus == RoomStatus.Available),
            occupied = rooms.Count(r => r.CurrentStatus == RoomStatus.Occupied),
            cleaning = rooms.Count(r => r.CurrentStatus == RoomStatus.Cleaning),
            maintenance = rooms.Count(r => r.CurrentStatus == RoomStatus.Maintenance),
            list = rooms
        };

        // ── Queue (Walk-in วันนี้) ──────────────────
        var walkIns = await db.WalkIns
            .Where(w => w.TenantId == tenantId
                && w.BranchId == branchId
                && w.ArrivalTime >= todayStartUtc
                && w.ArrivalTime <= todayEndUtc
                && w.DeletedAt == null)
            .Select(w => new
            {
                w.Id,
                w.QueueNo,
                w.Status,
                w.ArrivalTime,
                w.StartTime,
                w.EndTime,
                w.EstimatedWaitMins,
                Customer = new
                {
                    w.Customer.DisplayName,
                    w.Customer.Phone
                },
                ServiceCount = w.Items.Count
            })
            .OrderBy(w => w.ArrivalTime)
            .ToListAsync();

        var queueSummary = new
        {
            totalToday = walkIns.Count,
            waiting = walkIns.Count(w => w.Status == WalkInStatus.Waiting),
            inService = walkIns.Count(w => w.Status == WalkInStatus.InService),
            completed = walkIns.Count(w => w.Status == WalkInStatus.Completed),
            cancelled = walkIns.Count(w => w.Status == WalkInStatus.Cancelled),
            waitingList = walkIns.Where(w => w.Status == WalkInStatus.Waiting).ToList(),
            inServiceList = walkIns.Where(w => w.Status == WalkInStatus.InService).ToList()
        };

        // ── Bookings วันนี้ ──────────────────────────
        var bookings = await db.Bookings
            .Where(b => b.TenantId == tenantId
                && b.BranchId == branchId
                && b.BookingDate == today
                && b.DeletedAt == null)
            .Select(b => new
            {
                b.Id,
                b.BookingNo,
                b.StartTime,
                b.EndTime,
                b.TotalAmount,
                b.Status,
                Customer = new
                {
                    b.Customer.DisplayName,
                    b.Customer.Phone
                },
                ItemCount = b.Items.Count
            })
            .OrderBy(b => b.StartTime)
            .ToListAsync();

        var bookingSummary = new
        {
            total = bookings.Count,
            pending = bookings.Count(b => b.Status == BookingStatus.Pending),
            confirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed),
            inProgress = bookings.Count(b => b.Status == BookingStatus.InProgress),
            completed = bookings.Count(b => b.Status == BookingStatus.Completed),
            cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            noShow = bookings.Count(b => b.Status == BookingStatus.NoShow),
            upcomingList = bookings
                .Where(b => b.Status is BookingStatus.Pending or BookingStatus.Confirmed)
                .ToList()
        };

        // ── Revenue วันนี้ ───────────────────────────
        var payments = await db.Payments
            .Where(p => p.TenantId == tenantId
                && p.BranchId == branchId
                && p.Status == PaymentStatus.Paid
                && p.PaidAt >= todayStartUtc
                && p.PaidAt <= todayEndUtc
                && p.DeletedAt == null)
            .ToListAsync();

        var revenueSummary = new
        {
            totalReceipts = payments.Count,
            totalRevenue = payments.Sum(p => p.TotalAmount),
            totalDiscount = payments.Sum(p => p.DiscountAmount),
            byMethod = payments
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new
                {
                    method = g.Key.ToString(),
                    count = g.Count(),
                    amount = g.Sum(p => p.TotalAmount)
                })
                .ToList()
        };

        // ── Monthly Revenue ──────────────────────────
        var thisMonth = new DateTime(today.Year, today.Month, 1);
        var monthStartUtc = thisMonth.AddHours(-7);
        var monthPayments = await db.Payments
            .Where(p => p.TenantId == tenantId
                && p.BranchId == branchId
                && p.Status == PaymentStatus.Paid
                && p.PaidAt >= monthStartUtc
                && p.PaidAt <= todayEndUtc
                && p.DeletedAt == null)
            .ToListAsync();

        var monthlyRevenue = new
        {
            totalRevenue = monthPayments.Sum(p => p.TotalAmount),
            totalReceipts = monthPayments.Count,
        };

        // ── Trends (เทียบช่วงก่อนหน้า เพื่อแสดง % เติบโตบนการ์ด) ──
        var yesterday = today.AddDays(-1);
        var yStartUtc = yesterday.ToDateTime(TimeOnly.MinValue).AddHours(-7);
        var yEndUtc = yesterday.ToDateTime(TimeOnly.MaxValue).AddHours(-7);
        var yRevenue = await db.Payments
            .Where(p => p.TenantId == tenantId && p.BranchId == branchId && p.Status == PaymentStatus.Paid
                && p.PaidAt >= yStartUtc && p.PaidAt <= yEndUtc && p.DeletedAt == null)
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
        var yWalkIns = await db.WalkIns.CountAsync(w => w.TenantId == tenantId && w.BranchId == branchId
            && w.ArrivalTime >= yStartUtc && w.ArrivalTime <= yEndUtc && w.DeletedAt == null);
        var yBookings = await db.Bookings.CountAsync(b => b.TenantId == tenantId && b.BranchId == branchId
            && b.BookingDate == yesterday && b.DeletedAt == null);

        // เดือนก่อน ช่วงเดียวกัน (วันที่ 1 ถึงวันเดียวกันของเดือน)
        var lastMonth = thisMonth.AddMonths(-1);
        var lastMonthStartUtc = lastMonth.AddHours(-7);
        var lmDay = Math.Min(today.Day, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));
        var lastMonthEndUtc = new DateTime(lastMonth.Year, lastMonth.Month, lmDay)
            .AddDays(1).AddHours(-7);
        var lastMonthRevenue = await db.Payments
            .Where(p => p.TenantId == tenantId && p.BranchId == branchId && p.Status == PaymentStatus.Paid
                && p.PaidAt >= lastMonthStartUtc && p.PaidAt < lastMonthEndUtc && p.DeletedAt == null)
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;

        static double Pct(decimal curr, decimal prev)
            => prev <= 0 ? (curr > 0 ? 100 : 0) : Math.Round((double)((curr - prev) / prev) * 100, 1);

        var trends = new
        {
            revenueDay = Pct(revenueSummary.totalRevenue, yRevenue),
            revenueMonth = Pct(monthlyRevenue.totalRevenue, lastMonthRevenue),
            customers = Pct(queueSummary.totalToday, yWalkIns),
            bookings = Pct(bookingSummary.total, yBookings),
        };

        // ── Plan Info ────────────────────────────────
        var tenant = await db.Tenants.FindAsync(tenantId);
        var planInfo = new
        {
            planType = tenant?.PlanType ?? "Free",
            trialEndsAt = tenant?.TrialEndsAt,
        };

        // ── Snapshot รวม ────────────────────────────
        var snapshot = new
        {
            date = today,
            generatedAt = ThaiTime.Now,
            therapists = therapistSummary,
            rooms = roomSummary,
            queue = queueSummary,
            bookings = bookingSummary,
            revenue = revenueSummary,
            monthlyRevenue,
            trends,
            plan = planInfo,
        };

        return Ok(snapshot);
    }

    /// <summary>
    /// GET /api/dashboard/schedule?date=2024-05-17
    /// ตารางงานหมอนวดรายวัน สำหรับ Timeline view
    /// </summary>
    [HttpGet("schedule")]
    [RequirePermission(PermissionCodes.DashboardView)]
    public async Task<IActionResult> GetSchedule([FromQuery] DateOnly? date)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var targetDate = date ?? ThaiTime.Today;
        var startUtc = targetDate.ToDateTime(TimeOnly.MinValue).AddHours(-7);
        var endUtc = targetDate.ToDateTime(TimeOnly.MaxValue).AddHours(-7);

        var therapists = await db.Therapists
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId
                && t.IsActive && t.DeletedAt == null)
            .Select(t => new { t.Id, t.DisplayName, t.AvatarUrl, t.CurrentStatus })
            .OrderBy(t => t.DisplayName)
            .ToListAsync();

        // Walk-in items วันนี้
        var walkInItems = await db.WalkInItems
            .Where(i => i.TenantId == tenantId
                && i.TherapistId != null
                && i.StartTime != null
                && i.StartTime >= startUtc && i.StartTime <= endUtc
                && i.DeletedAt == null)
            .Select(i => new
            {
                ItemId = i.Id,
                TherapistId = i.TherapistId!.Value,
                i.StartTime,
                i.EndTime,
                ServiceName = i.Service.Name,
                ServiceCategory = i.Service.Category.Name,
                CustomerName = i.WalkIn.Customer.DisplayName,
                Source = "walkin",
            })
            .ToListAsync();

        // Booking items วันนี้
        var bookingItems = await db.BookingItems
            .Where(i => i.TenantId == tenantId
                && i.TherapistId != null
                && i.Booking.BookingDate == targetDate
                && i.Booking.DeletedAt == null
                && i.DeletedAt == null)
            .Select(i => new
            {
                ItemId = i.Id,
                TherapistId = i.TherapistId!.Value,
                StartTime = (DateTime?)i.Booking.BookingDate.ToDateTime(i.StartTime).AddHours(-7),
                EndTime = (DateTime?)i.Booking.BookingDate.ToDateTime(i.EndTime).AddHours(-7),
                ServiceName = i.Service.Name,
                ServiceCategory = i.Service.Category.Name,
                CustomerName = i.Booking.Customer.DisplayName,
                Source = "booking",
            })
            .ToListAsync();

        var allItems = walkInItems.Cast<object>().Concat(bookingItems).ToList();

        var schedule = therapists.Select(t =>
        {
            var tItems = allItems
                .Cast<dynamic>()
                .Where(i => (Guid)i.TherapistId == t.Id)
                .Select(i => new
                {
                    id = (Guid)i.ItemId,
                    startTime = (DateTime?)i.StartTime,
                    endTime = (DateTime?)i.EndTime,
                    serviceName = (string)i.ServiceName,
                    serviceCategory = (string)i.ServiceCategory,
                    customerName = (string)i.CustomerName,
                    source = (string)i.Source,
                })
                .OrderBy(i => i.startTime)
                .ToList();

            return new
            {
                t.Id,
                t.DisplayName,
                t.AvatarUrl,
                t.CurrentStatus,
                items = tItems,
            };
        });

        return Ok(new { date = targetDate, therapists = schedule });
    }

    /// <summary>
    /// PATCH /api/dashboard/schedule/reschedule
    /// ย้ายเวลา/เปลี่ยนหมอนวดของคิว (walk-in หรือ booking) จากการลากในตาราง Gantt
    /// </summary>
    [HttpPatch("schedule/reschedule")]
    [RequirePermission(PermissionCodes.DashboardView)]
    public async Task<IActionResult> RescheduleItem([FromBody] RescheduleRequest req)
    {
        var tenantId = User.GetTenantId();

        if (!TimeOnly.TryParse(req.StartTime, out var startLocal))
            return BadRequest(new { message = "รูปแบบเวลาไม่ถูกต้อง (HH:mm)" });

        if (req.Source == "walkin")
        {
            var item = await db.WalkInItems
                .FirstOrDefaultAsync(i => i.Id == req.ItemId && i.TenantId == tenantId && i.DeletedAt == null);
            if (item == null) return NotFound(new { message = "ไม่พบคิว" });

            // วันของคิวเดิม (local) แล้วตั้งเวลาเริ่มใหม่ → แปลงกลับเป็น UTC
            var localDate = DateOnly.FromDateTime((item.StartTime ?? DateTime.UtcNow.AddHours(7)).AddHours(7));
            var startUtc = localDate.ToDateTime(startLocal).AddHours(-7);
            item.StartTime = startUtc;
            item.EndTime = startUtc.AddMinutes(item.DurationMins);
            if (req.TherapistId.HasValue) item.TherapistId = req.TherapistId.Value;

            await db.SaveChangesAsync();
            try {
                var w = await db.WalkIns.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == item.WalkInId);
                var th = req.TherapistId.HasValue ? await db.Therapists.FirstOrDefaultAsync(t => t.Id == req.TherapistId.Value) : null;
                await NotifyRescheduleAsync(tenantId, w?.Customer?.DisplayName ?? "ลูกค้า", th?.DisplayName ?? "หมอนวด", th?.UserId, req.StartTime, w?.QueueNo ?? "คิว");
            } catch { }
            return Ok(new { message = "ย้ายคิวแล้ว", source = "walkin" });
        }

        if (req.Source == "booking")
        {
            var item = await db.BookingItems
                .FirstOrDefaultAsync(i => i.Id == req.ItemId && i.TenantId == tenantId && i.DeletedAt == null);
            if (item == null) return NotFound(new { message = "ไม่พบคิว" });

            item.StartTime = startLocal;
            item.EndTime = startLocal.Add(TimeSpan.FromMinutes(item.DurationMins));
            if (req.TherapistId.HasValue) item.TherapistId = req.TherapistId.Value;

            await db.SaveChangesAsync();
            try {
                var bk = await db.Bookings.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == item.BookingId);
                var th = req.TherapistId.HasValue ? await db.Therapists.FirstOrDefaultAsync(t => t.Id == req.TherapistId.Value) : null;
                await NotifyRescheduleAsync(tenantId, bk?.Customer?.DisplayName ?? "ลูกค้า", th?.DisplayName ?? "หมอนวด", th?.UserId, req.StartTime, bk?.BookingNo ?? "การจอง");
            } catch { }
            return Ok(new { message = "ย้ายคิวแล้ว", source = "booking" });
        }

        return BadRequest(new { message = "source ต้องเป็น walkin หรือ booking" });
    }

    /// <summary>
    /// POST /api/dashboard/broadcast
    /// Broadcast snapshot ไปยัง client ทุกคนในสาขา (ใช้ตอน manual refresh)
    /// </summary>
    [HttpPost("broadcast")]
    [RequirePermission(PermissionCodes.DashboardView)]
    public async Task<IActionResult> BroadcastSnapshot()
    {
        var branchId = User.GetBranchId();

        // ดึง snapshot แล้ว broadcast
        var snapshotResult = await GetSnapshot() as OkObjectResult;
        if (snapshotResult?.Value == null)
            return StatusCode(500, new { message = "Failed to get snapshot" });

        await realtime.NotifyDashboardSnapshotAsync(branchId, snapshotResult.Value);

        return Ok(new { message = "Dashboard snapshot broadcasted" });
    }
}

public record RescheduleRequest(string Source, Guid ItemId, string StartTime, Guid? TherapistId);