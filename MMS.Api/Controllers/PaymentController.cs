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
[Route("api/[controller]")]
[Authorize]
public class PaymentController(AppDbContext db, PaymentService paymentService, ActivityTimelineService timeline) : ControllerBase
{
    // ──────────────────────────────────────────────
    // LIST / GET
    // ──────────────────────────────────────────────

    [HttpGet]
    [RequirePermission(PermissionCodes.PaymentView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateOnly? date,
        [FromQuery] PaymentStatus? status,
        [FromQuery] PaymentMethod? method,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var query = db.Payments
            .Where(p => p.TenantId == tenantId
                && p.BranchId == branchId
                && p.DeletedAt == null);

        if (date.HasValue)
        {
            var from = date.Value.ToDateTime(TimeOnly.MinValue);
            var to = date.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(p => p.PaidAt >= from && p.PaidAt <= to);
        }

        if (status.HasValue)
            query = query.Where(p => p.Status == status);

        if (method.HasValue)
            query = query.Where(p => p.PaymentMethod == method);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.PaidAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id, p.ReceiptNo, p.PaidAt,
                p.SubTotal, p.DiscountAmount, p.TotalAmount,
                p.PaidAmount, p.ChangeAmount,
                p.PaymentMethod, p.Status,
                Customer = new { p.Customer.DisplayName, p.Customer.Phone },
                p.BookingId, p.WalkInId
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.PaymentView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var payment = await db.Payments
            .Include(p => p.Customer)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id
                && p.TenantId == tenantId && p.DeletedAt == null);

        if (payment == null)
            return NotFound(new { message = "Payment not found" });

        return Ok(new
        {
            payment.Id, payment.ReceiptNo, payment.PaidAt,
            payment.SubTotal, payment.DiscountAmount,
            payment.TotalAmount, payment.PaidAmount, payment.ChangeAmount,
            payment.PaymentMethod, payment.Status, payment.Notes,
            Customer = new
            {
                payment.Customer.Id,
                payment.Customer.DisplayName,
                payment.Customer.Phone
            },
            payment.BookingId, payment.WalkInId,
            Items = payment.Items.Select(i => new
            {
                i.ServiceName, i.TherapistName,
                i.UnitPrice, i.Quantity,
                i.DiscountAmount, i.LineTotal,
                i.CommissionAmount, i.RefType
            })
        });
    }

    // ──────────────────────────────────────────────
    // CREATE PAYMENT
    // ──────────────────────────────────────────────

    [HttpPost]
    [RequirePermission(PermissionCodes.PaymentCreate)]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var cashierId = User.GetUserId();

        var result = await paymentService.CreatePaymentAsync(
            tenantId, branchId, cashierId, req);

        if (!result.Success)
            return BadRequest(new { message = result.Error });

        await timeline.LogAsync("payment", "Payment", result.PaymentId!.Value,
            $"รับชำระเงิน {result.TotalAmount:N0} บาท", result.ReceiptNo);

        return Ok(new
        {
            message = "Payment successful",
            paymentId = result.PaymentId,
            receiptNo = result.ReceiptNo,
            totalAmount = result.TotalAmount,
            changeAmount = result.ChangeAmount
        });
    }

    // ──────────────────────────────────────────────
    // REFUND
    // ──────────────────────────────────────────────

    [HttpPatch("{id:guid}/refund")]
    [RequirePermission(PermissionCodes.PaymentRefund)]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundRequest req)
    {
        var (ok, err) = await paymentService.RefundAsync(
            id, User.GetTenantId(), req.Reason);
        return ok ? Ok(new { message = "Payment refunded" })
                  : BadRequest(new { message = err });
    }

    // ──────────────────────────────────────────────
    // SUMMARY (ยอดรายวัน)
    // ──────────────────────────────────────────────

    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.ReportView)]
    public async Task<IActionResult> GetDailySummary([FromQuery] DateOnly? date)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from = targetDate.ToDateTime(TimeOnly.MinValue).AddHours(-7); // วันไทย → ช่วง UTC
        var to = targetDate.ToDateTime(TimeOnly.MaxValue).AddHours(-7);

        var payments = await db.Payments
            .Where(p => p.TenantId == tenantId
                && p.BranchId == branchId
                && p.Status == PaymentStatus.Paid
                && p.PaidAt >= from && p.PaidAt <= to
                && p.DeletedAt == null)
            .ToListAsync();

        var summary = new
        {
            date = targetDate,
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
        };

        return Ok(summary);
    }
}

public record RefundRequest(string? Reason);
