using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Enums;
using MMS.Domain.Helper;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportController(AppDbContext db) : ControllerBase
{
    // ─────────────────────────────────────────────────────────
    // 1. Revenue Report — ยอดขายรายวัน/เดือน
    // GET /api/report/revenue?year=2026&month=6&groupBy=day
    // ─────────────────────────────────────────────────────────
    [HttpGet("revenue")]
    [RequirePermission(PermissionCodes.ReportView)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string groupBy = "day") // day | month
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var now = ThaiTime.Now;

        year ??= now.Year;
        month ??= now.Month;

        // กำหนด date range
        DateTime fromUtc, toUtc;
        if (groupBy == "month")
        {
            // ทั้งปี
            fromUtc = new DateTime(year.Value, 1, 1).AddHours(-7);
            toUtc = new DateTime(year.Value, 12, 31, 23, 59, 59).AddHours(-7);
        }
        else
        {
            // ทั้งเดือน
            var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
            fromUtc = new DateTime(year.Value, month.Value, 1).AddHours(-7);
            toUtc = new DateTime(year.Value, month.Value, daysInMonth, 23, 59, 59).AddHours(-7);
        }

        var payments = await db.Payments
            .Where(p => p.TenantId == tenantId
                && p.BranchId == branchId
                && p.Status == PaymentStatus.Paid
                && p.PaidAt >= fromUtc
                && p.PaidAt <= toUtc
                && p.DeletedAt == null)
            .ToListAsync();

        // Group by day หรือ month
        List<object> series;
        if (groupBy == "month")
        {
            series = payments
                .GroupBy(p => ThaiTime.FromUtc(p.PaidAt!.Value).Month)
                .OrderBy(g => g.Key)
                .Select(g => (object)new
                {
                    period = $"{year}-{g.Key:D2}",
                    label = $"เดือน {g.Key}",
                    revenue = g.Sum(p => p.TotalAmount),
                    receipts = g.Count(),
                    discount = g.Sum(p => p.DiscountAmount),
                })
                .ToList();
        }
        else
        {
            series = payments
                .GroupBy(p => ThaiTime.FromUtc(p.PaidAt!.Value).Date)
                .OrderBy(g => g.Key)
                .Select(g => (object)new
                {
                    period = g.Key.ToString("yyyy-MM-dd"),
                    label = g.Key.ToString("dd MMM"),
                    revenue = g.Sum(p => p.TotalAmount),
                    receipts = g.Count(),
                    discount = g.Sum(p => p.DiscountAmount),
                })
                .ToList();
        }

        // Summary
        var summary = new
        {
            totalRevenue = payments.Sum(p => p.TotalAmount),
            totalReceipts = payments.Count,
            totalDiscount = payments.Sum(p => p.DiscountAmount),
            avgPerReceipt = payments.Count > 0
                ? payments.Sum(p => p.TotalAmount) / payments.Count
                : 0,
            byMethod = payments
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new
                {
                    method = g.Key.ToString(),
                    amount = g.Sum(p => p.TotalAmount),
                    count = g.Count()
                })
                .ToList()
        };

        return Ok(new { year, month, groupBy, summary, series });
    }

    // ─────────────────────────────────────────────────────────
    // 2. Therapist Performance — commission + จำนวนลูกค้า
    // GET /api/report/therapist-performance?year=2026&month=6
    // ─────────────────────────────────────────────────────────
    [HttpGet("therapist-performance")]
    [RequirePermission(PermissionCodes.ReportView)]
    public async Task<IActionResult> GetTherapistPerformance(
        [FromQuery] int? year,
        [FromQuery] int? month)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var now = ThaiTime.Now;

        year ??= now.Year;
        month ??= now.Month;

        var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
        var fromUtc = new DateTime(year.Value, month.Value, 1).AddHours(-7);
        var toUtc = new DateTime(year.Value, month.Value, daysInMonth, 23, 59, 59).AddHours(-7);

        // BookingItems
        var bookingItems = await db.BookingItems
            .Where(bi => bi.TenantId == tenantId
                && bi.TherapistId != null
                && bi.Booking.BranchId == branchId
                && bi.Booking.Status == BookingStatus.Completed
                && bi.Booking.BookingDate >= DateOnly.FromDateTime(fromUtc.AddHours(7))
                && bi.Booking.BookingDate <= DateOnly.FromDateTime(toUtc.AddHours(7))
                && bi.Booking.DeletedAt == null
                && bi.DeletedAt == null)
            .Include(bi => bi.Therapist)
            .Include(bi => bi.Service)
            .ToListAsync();

        // WalkInItems
        var walkInItems = await db.WalkInItems
            .Where(wi => wi.TenantId == tenantId
                && wi.TherapistId != null
                && wi.WalkIn.BranchId == branchId
                && wi.WalkIn.Status == WalkInStatus.Completed
                && wi.WalkIn.ArrivalTime >= fromUtc
                && wi.WalkIn.ArrivalTime <= toUtc
                && wi.WalkIn.DeletedAt == null
                && wi.DeletedAt == null)
            .Include(wi => wi.Therapist)
            .Include(wi => wi.Service)
            .ToListAsync();

        // รวม therapist performance
        var therapistIds = bookingItems.Select(bi => bi.TherapistId!.Value)
            .Concat(walkInItems.Select(wi => wi.TherapistId!.Value))
            .Distinct()
            .ToList();

        var performance = therapistIds.Select(tid =>
        {
            var bItems = bookingItems.Where(bi => bi.TherapistId == tid).ToList();
            var wItems = walkInItems.Where(wi => wi.TherapistId == tid).ToList();
            var therapist = bItems.FirstOrDefault()?.Therapist
                ?? wItems.FirstOrDefault()?.Therapist;

            var totalRevenue = bItems.Sum(bi => bi.Price) + wItems.Sum(wi => wi.Price);
            var totalCommission = bItems.Sum(bi => bi.CommissionAmount ?? 0)
                + wItems.Sum(wi => wi.CommissionAmount ?? 0);
            var totalCustomers = bItems.Count + wItems.Count;
            var totalMins = bItems.Sum(bi => bi.DurationMins) + wItems.Sum(wi => wi.DurationMins);

            return new
            {
                therapistId = tid,
                displayName = therapist?.DisplayName ?? "Unknown",
                code = therapist?.Code,
                totalCustomers,
                totalMins,
                totalRevenue,
                totalCommission,
                avgRevenuePerCustomer = totalCustomers > 0 ? totalRevenue / totalCustomers : 0,
                topServices = bItems.Select(bi => bi.Service?.Name)
                    .Concat(wItems.Select(wi => wi.Service?.Name))
                    .Where(n => n != null)
                    .GroupBy(n => n)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => new { service = g.Key, count = g.Count() })
                    .ToList()
            };
        })
        .OrderByDescending(p => p.totalRevenue)
        .ToList();

        return Ok(new { year, month, performance });
    }

    // ─────────────────────────────────────────────────────────
    // 3. Popular Services — บริการยอดนิยม
    // GET /api/report/popular-services?year=2026&month=6
    // ─────────────────────────────────────────────────────────
    [HttpGet("popular-services")]
    [RequirePermission(PermissionCodes.ReportView)]
    public async Task<IActionResult> GetPopularServices(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int top = 10)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var now = ThaiTime.Now;

        year ??= now.Year;
        month ??= now.Month;

        var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
        var fromDate = new DateOnly(year.Value, month.Value, 1);
        var toDate = new DateOnly(year.Value, month.Value, daysInMonth);
        var fromUtc = new DateTime(year.Value, month.Value, 1).AddHours(-7);
        var toUtc = new DateTime(year.Value, month.Value, daysInMonth, 23, 59, 59).AddHours(-7);

        // BookingItems
        var bookingItems = await db.BookingItems
            .Where(bi => bi.TenantId == tenantId
                && bi.Booking.BranchId == branchId
                && bi.Booking.Status == BookingStatus.Completed
                && bi.Booking.BookingDate >= fromDate
                && bi.Booking.BookingDate <= toDate
                && bi.Booking.DeletedAt == null
                && bi.DeletedAt == null)
            .Include(bi => bi.Service)
            .ToListAsync();

        // WalkInItems
        var walkInItems = await db.WalkInItems
            .Where(wi => wi.TenantId == tenantId
                && wi.WalkIn.BranchId == branchId
                && wi.WalkIn.Status == WalkInStatus.Completed
                && wi.WalkIn.ArrivalTime >= fromUtc
                && wi.WalkIn.ArrivalTime <= toUtc
                && wi.WalkIn.DeletedAt == null
                && wi.DeletedAt == null)
            .Include(wi => wi.Service)
            .ToListAsync();

        // รวมและ group by service
        var allItems = bookingItems
            .Select(bi => new { ServiceId = bi.ServiceId, Name = bi.Service?.Name ?? "", Price = bi.Price, DurationMins = bi.DurationMins })
            .Concat(walkInItems
                .Select(wi => new { ServiceId = wi.ServiceId, Name = wi.Service?.Name ?? "", Price = wi.Price, DurationMins = wi.DurationMins }))
            .ToList();

        var popular = allItems
            .GroupBy(i => new { i.ServiceId, i.Name })
            .Select(g => new
            {
                serviceId = g.Key.ServiceId,
                name = g.Key.Name,
                count = g.Count(),
                totalRevenue = g.Sum(i => i.Price),
                totalMins = g.Sum(i => i.DurationMins),
                avgPrice = g.Average(i => i.Price)
            })
            .OrderByDescending(s => s.count)
            .Take(top)
            .ToList();

        return Ok(new { year, month, popular });
    }

    // ─────────────────────────────────────────────────────────
    // 4. Summary — ภาพรวม Booking + WalkIn
    // GET /api/report/summary?year=2026&month=6
    // ─────────────────────────────────────────────────────────
    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.ReportView)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int? year,
        [FromQuery] int? month)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var now = ThaiTime.Now;

        year ??= now.Year;
        month ??= now.Month;

        var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);
        var fromDate = new DateOnly(year.Value, month.Value, 1);
        var toDate = new DateOnly(year.Value, month.Value, daysInMonth);
        var fromUtc = new DateTime(year.Value, month.Value, 1).AddHours(-7);
        var toUtc = new DateTime(year.Value, month.Value, daysInMonth, 23, 59, 59).AddHours(-7);

        // Bookings
        var bookings = await db.Bookings
            .Where(b => b.TenantId == tenantId
                && b.BranchId == branchId
                && b.BookingDate >= fromDate
                && b.BookingDate <= toDate
                && b.DeletedAt == null)
            .ToListAsync();

        // WalkIns
        var walkIns = await db.WalkIns
            .Where(w => w.TenantId == tenantId
                && w.BranchId == branchId
                && w.ArrivalTime >= fromUtc
                && w.ArrivalTime <= toUtc
                && w.DeletedAt == null)
            .ToListAsync();

        // Revenue
        var payments = await db.Payments
            .Where(p => p.TenantId == tenantId
                && p.BranchId == branchId
                && p.Status == PaymentStatus.Paid
                && p.PaidAt >= fromUtc
                && p.PaidAt <= toUtc
                && p.DeletedAt == null)
            .ToListAsync();

        var bookingSummary = new
        {
            total = bookings.Count,
            completed = bookings.Count(b => b.Status == BookingStatus.Completed),
            cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            noShow = bookings.Count(b => b.Status == BookingStatus.NoShow),
            pending = bookings.Count(b => b.Status == BookingStatus.Pending),
            confirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed),
            completionRate = bookings.Count > 0
                ? Math.Round((double)bookings.Count(b => b.Status == BookingStatus.Completed) / bookings.Count * 100, 1)
                : 0
        };

        var walkInSummary = new
        {
            total = walkIns.Count,
            completed = walkIns.Count(w => w.Status == WalkInStatus.Completed),
            cancelled = walkIns.Count(w => w.Status == WalkInStatus.Cancelled),
            completionRate = walkIns.Count > 0
                ? Math.Round((double)walkIns.Count(w => w.Status == WalkInStatus.Completed) / walkIns.Count * 100, 1)
                : 0
        };

        var revenueSummary = new
        {
            total = payments.Sum(p => p.TotalAmount),
            fromBooking = payments.Where(p => p.BookingId != null).Sum(p => p.TotalAmount),
            fromWalkIn = payments.Where(p => p.WalkInId != null).Sum(p => p.TotalAmount),
            receipts = payments.Count
        };

        // เปรียบเทียบกับเดือนก่อน
        var prevMonth = month.Value == 1 ? 12 : month.Value - 1;
        var prevYear = month.Value == 1 ? year.Value - 1 : year.Value;
        var prevDays = DateTime.DaysInMonth(prevYear, prevMonth);
        var prevFromUtc = new DateTime(prevYear, prevMonth, 1).AddHours(-7);
        var prevToUtc = new DateTime(prevYear, prevMonth, prevDays, 23, 59, 59).AddHours(-7);

        var prevRevenue = await db.Payments
            .Where(p => p.TenantId == tenantId
                && p.BranchId == branchId
                && p.Status == PaymentStatus.Paid
                && p.PaidAt >= prevFromUtc
                && p.PaidAt <= prevToUtc
                && p.DeletedAt == null)
            .SumAsync(p => p.TotalAmount);

        var revenueGrowth = prevRevenue > 0
            ? Math.Round((double)((revenueSummary.total - prevRevenue) / prevRevenue * 100), 1)
            : 0;

        return Ok(new
        {
            year,
            month,
            bookings = bookingSummary,
            walkIns = walkInSummary,
            revenue = revenueSummary,
            comparison = new
            {
                prevMonthRevenue = prevRevenue,
                revenueGrowth,
                trend = revenueGrowth >= 0 ? "up" : "down"
            }
        });
    }
}