using Microsoft.EntityFrameworkCore;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;

namespace MMS.Infrastructure.Persistence.Services;

public class PaymentService(AppDbContext db)
{
    // ──────────────────────────────────────────────
    // CREATE PAYMENT (จาก Booking หรือ WalkIn)
    // ──────────────────────────────────────────────

    public async Task<PaymentResult> CreatePaymentAsync(
        Guid tenantId, Guid branchId, Guid cashierId,
        CreatePaymentRequest req)
    {
        // ตรวจว่าจ่ายจาก Booking หรือ WalkIn
        if (req.BookingId == null && req.WalkInId == null)
            return PaymentResult.Fail("Must specify BookingId or WalkInId");

        if (req.BookingId != null && req.WalkInId != null)
            return PaymentResult.Fail("Cannot specify both BookingId and WalkInId");

        Guid customerId;
        decimal subTotal;
        var paymentItems = new List<PaymentItemData>();

        // ── จาก Booking ──
        if (req.BookingId != null)
        {
            var booking = await db.Bookings
                .Include(b => b.Items)
                    .ThenInclude(i => i.Service)
                .Include(b => b.Items)
                    .ThenInclude(i => i.Therapist)
                .FirstOrDefaultAsync(b => b.Id == req.BookingId
                    && b.TenantId == tenantId && b.DeletedAt == null);

            if (booking == null)
                return PaymentResult.Fail("Booking not found");

            if (booking.Status != BookingStatus.Completed)
                return PaymentResult.Fail("Booking must be completed before payment");

            if (booking.Payment != null)
                return PaymentResult.Fail("Booking already paid");

            customerId = booking.CustomerId;
            subTotal = booking.TotalAmount;

            foreach (var item in booking.Items.OrderBy(i => i.SortOrder))
            {
                paymentItems.Add(new PaymentItemData(
                    "BookingItem", item.Id,
                    item.Service.Name,
                    item.Therapist?.DisplayName,
                    item.Price, 1, 0,
                    item.CommissionAmount));
            }
        }
        // ── จาก WalkIn ──
        else
        {
            var walkIn = await db.WalkIns
                .Include(w => w.Items)
                    .ThenInclude(i => i.Service)
                .Include(w => w.Items)
                    .ThenInclude(i => i.Therapist)
                .FirstOrDefaultAsync(w => w.Id == req.WalkInId
                    && w.TenantId == tenantId && w.DeletedAt == null);

            if (walkIn == null)
                return PaymentResult.Fail("WalkIn not found");

            if (walkIn.Status != WalkInStatus.Completed)
                return PaymentResult.Fail("WalkIn must be completed before payment");

            if (walkIn.Payment != null)
                return PaymentResult.Fail("WalkIn already paid");

            customerId = walkIn.CustomerId;
            subTotal = walkIn.TotalAmount ?? 0;

            foreach (var item in walkIn.Items.OrderBy(i => i.SortOrder))
            {
                paymentItems.Add(new PaymentItemData(
                    "WalkInItem", item.Id,
                    item.Service.Name,
                    item.Therapist?.DisplayName,
                    item.Price, 1, 0,
                    item.CommissionAmount));
            }
        }

        // คำนวณ discount และ total
        var discountAmount = req.DiscountAmount;
        var totalAmount = subTotal - discountAmount;
        var changeAmount = req.PaidAmount - totalAmount;

        if (req.PaidAmount < totalAmount)
            return PaymentResult.Fail(
                $"Paid amount {req.PaidAmount} is less than total {totalAmount}");

        // สร้าง Receipt No
        var receiptNo = await GenerateReceiptNoAsync(tenantId);

        var payment = new Payment
        {
            TenantId = tenantId,
            BranchId = branchId,
            ReceiptNo = receiptNo,
            BookingId = req.BookingId,
            WalkInId = req.WalkInId,
            CustomerId = customerId,
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            TotalAmount = totalAmount,
            PaidAmount = req.PaidAmount,
            ChangeAmount = changeAmount,
            PaymentMethod = req.PaymentMethod,
            Status = PaymentStatus.Paid,
            PaidAt = DateTime.UtcNow.AddHours(7),
            CashierId = cashierId,
            Notes = req.Notes
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        // สร้าง PaymentItems
        foreach (var item in paymentItems)
        {
            var lineDiscount = discountAmount > 0 && paymentItems.Count > 0
                ? Math.Round(discountAmount / paymentItems.Count, 2)
                : 0;

            db.PaymentItems.Add(new PaymentItem
            {
                TenantId = tenantId,
                PaymentId = payment.Id,
                RefType = item.RefType,
                RefId = item.RefId,
                ServiceName = item.ServiceName,
                TherapistName = item.TherapistName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                DiscountAmount = lineDiscount,
                LineTotal = (item.UnitPrice * item.Quantity) - lineDiscount,
                CommissionAmount = item.CommissionAmount
            });
        }

        await db.SaveChangesAsync();
        return PaymentResult.Ok(payment.Id, receiptNo, totalAmount, changeAmount);
    }

    // ──────────────────────────────────────────────
    // REFUND
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> RefundAsync(
        Guid paymentId, Guid tenantId, string? reason)
    {
        var payment = await db.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId
                && p.TenantId == tenantId && p.DeletedAt == null);

        if (payment == null) return (false, "Payment not found");
        if (payment.Status != PaymentStatus.Paid)
            return (false, $"Cannot refund payment with status {payment.Status}");

        payment.Status = PaymentStatus.Refunded;
        payment.Notes = string.IsNullOrWhiteSpace(reason)
            ? payment.Notes
            : $"{payment.Notes} [Refunded: {reason}]";

        await db.SaveChangesAsync();
        return (true, null);
    }

    // ──────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────

    private async Task<string> GenerateReceiptNoAsync(Guid tenantId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var prefix = $"RC{today:yyMMdd}";
        var count = await db.Payments
            .CountAsync(p => p.TenantId == tenantId
                && p.ReceiptNo.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
}

// DTOs
public record CreatePaymentRequest(
    Guid? BookingId,
    Guid? WalkInId,
    PaymentMethod PaymentMethod,
    decimal PaidAmount,
    decimal DiscountAmount = 0,
    string? Notes = null);

public record PaymentItemData(
    string RefType,
    Guid? RefId,
    string ServiceName,
    string? TherapistName,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount,
    decimal? CommissionAmount);

public record PaymentResult(
    bool Success, Guid? PaymentId, string? ReceiptNo,
    decimal? TotalAmount, decimal? ChangeAmount, string? Error)
{
    public static PaymentResult Ok(Guid id, string receiptNo, decimal total, decimal change)
        => new(true, id, receiptNo, total, change, null);
    public static PaymentResult Fail(string error)
        => new(false, null, null, null, null, error);
}
