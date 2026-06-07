using Microsoft.EntityFrameworkCore;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;

namespace MMS.Infrastructure.Persistence.Services;

public class BookingService(AppDbContext db, AvailabilityService availability)
{
    // ──────────────────────────────────────────────
    // CREATE BOOKING
    // ──────────────────────────────────────────────

    public async Task<BookingResult> CreateBookingAsync(
        Guid tenantId, Guid branchId, Guid createdBy,
        CreateBookingRequest req)
    {
        // 1. ตรวจ Customer
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == req.CustomerId
                && c.TenantId == tenantId && c.DeletedAt == null);
        if (customer == null)
            return BookingResult.Fail("Customer not found");

        // 2. ตรวจ Services และคำนวณเวลา
        var items = new List<BookingItemData>();
        var currentStart = req.StartTime;

        foreach (var item in req.Items.OrderBy(i => i.SortOrder))
        {
            var service = await db.Services
                .FirstOrDefaultAsync(s => s.Id == item.ServiceId
                    && s.TenantId == tenantId && s.IsActive && s.DeletedAt == null);
            if (service == null)
                return BookingResult.Fail($"Service {item.ServiceId} not found");

            var itemEnd = currentStart.AddMinutes(service.DurationMins);

            // 3. ตรวจ Therapist availability
            if (item.TherapistId.HasValue)
            {
                var therapist = await db.Therapists
                    .Include(t => t.Schedules)
                    .Include(t => t.Leaves)
                    .FirstOrDefaultAsync(t => t.Id == item.TherapistId
                        && t.TenantId == tenantId && t.DeletedAt == null);

                if (therapist == null)
                    return BookingResult.Fail($"Therapist not found");

                var slot = await availability.CheckTherapistAvailabilityAsync(
                    therapist, tenantId, req.BookingDate,
                    req.BookingDate.DayOfWeek, currentStart, itemEnd, service.DurationMins);

                if (slot == null)
                    return BookingResult.Fail(
                        $"{therapist.DisplayName} ไม่ว่างในช่วง {currentStart:HH\\:mm}-{itemEnd:HH\\:mm}");
            }

            // 4. ตรวจ Room availability
            if (item.RoomId.HasValue)
            {
                var roomBusy = await db.BookingItems
                    .AnyAsync(bi =>
                        bi.RoomId == item.RoomId
                        && bi.Booking.BookingDate == req.BookingDate
                        && bi.Booking.Status != BookingStatus.Cancelled
                        && bi.Booking.DeletedAt == null
                        && bi.DeletedAt == null
                        && bi.StartTime < itemEnd
                        && bi.EndTime > currentStart);

                if (roomBusy)
                    return BookingResult.Fail("Room ไม่ว่างในช่วงเวลาที่เลือก");
            }

            // คำนวณ commission
            decimal? commission = null;
            if (item.TherapistId.HasValue)
            {
                if (service.CommissionFixed.HasValue)
                    commission = service.CommissionFixed;
                else if (service.CommissionRate.HasValue)
                    commission = service.Price * service.CommissionRate.Value / 100;
            }

            items.Add(new BookingItemData(
                service, item.TherapistId, item.RoomId,
                currentStart, itemEnd, service.DurationMins,
                service.Price, commission,
                item.TherapistSelectionMode, item.SortOrder));

            currentStart = itemEnd.AddMinutes(service.BufferMins);
        }

        // 5. สร้าง Booking
        var bookingNo = await GenerateBookingNoAsync(tenantId);
        var totalDuration = items.Sum(i => i.DurationMins);
        var totalAmount = items.Sum(i => i.Price);
        var endTime = items.Max(i => i.EndTime);

        var booking = new Booking
        {
            TenantId = tenantId,
            BranchId = branchId,
            BookingNo = bookingNo,
            CustomerId = req.CustomerId,
            BookingDate = req.BookingDate,
            StartTime = req.StartTime,
            EndTime = endTime,
            TotalDurationMins = totalDuration,
            TotalAmount = totalAmount,
            DepositAmount = req.DepositAmount,
            Source = req.Source,
            Notes = req.Notes,
            Status = BookingStatus.Pending
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(); // ได้ Id แล้ว

        // 6. สร้าง BookingItems
        foreach (var item in items)
        {
            db.BookingItems.Add(new BookingItem
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                ServiceId = item.Service.Id,
                TherapistId = item.TherapistId,
                RoomId = item.RoomId,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                DurationMins = item.DurationMins,
                Price = item.Price,
                CommissionAmount = item.Commission,
                TherapistSelectionMode = item.TherapistSelectionMode,
                SortOrder = item.SortOrder
            });
        }

        await db.SaveChangesAsync();

        return BookingResult.Ok(booking.Id, bookingNo);
    }

    // ──────────────────────────────────────────────
    // CONFIRM / CANCEL / COMPLETE
    // ──────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> ConfirmAsync(
        Guid bookingId, Guid tenantId)
    {
        var booking = await GetBookingAsync(bookingId, tenantId);
        if (booking == null) return (false, "Booking not found");
        if (booking.Status != BookingStatus.Pending)
            return (false, $"Cannot confirm booking with status {booking.Status}");

        booking.Status = BookingStatus.Confirmed;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CancelAsync(
        Guid bookingId, Guid tenantId, Guid cancelledBy, string? reason)
    {
        var booking = await GetBookingAsync(bookingId, tenantId);
        if (booking == null) return (false, "Booking not found");

        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled)
            return (false, $"Cannot cancel booking with status {booking.Status}");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.CancelledBy = cancelledBy;
        booking.CancelReason = reason;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CompleteAsync(
        Guid bookingId, Guid tenantId)
    {
        var booking = await GetBookingAsync(bookingId, tenantId);
        if (booking == null) return (false, "Booking not found");
        if (booking.Status != BookingStatus.InProgress)
            return (false, $"Cannot complete booking with status {booking.Status}");

        booking.Status = BookingStatus.Completed;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> StartAsync(
        Guid bookingId, Guid tenantId)
    {
        var booking = await GetBookingAsync(bookingId, tenantId);
        if (booking == null) return (false, "Booking not found");
        if (booking.Status != BookingStatus.Confirmed)
            return (false, $"Cannot start booking with status {booking.Status}");

        booking.Status = BookingStatus.InProgress;
        await db.SaveChangesAsync();
        return (true, null);
    }

    // ──────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────

    private async Task<Booking?> GetBookingAsync(Guid id, Guid tenantId)
        => await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == id
                && b.TenantId == tenantId && b.DeletedAt == null);

    private async Task<string> GenerateBookingNoAsync(Guid tenantId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)); // Thai time
        var prefix = $"BK{today:yyMMdd}";
        var count = await db.Bookings
            .CountAsync(b => b.TenantId == tenantId
                && b.BookingNo.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
}

// ──────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────

public record CreateBookingRequest(
    Guid CustomerId,
    DateOnly BookingDate,
    TimeOnly StartTime,
    List<BookingItemRequest> Items,
    decimal DepositAmount = 0,
    BookingSource Source = BookingSource.Staff,
    string? Notes = null);

public record BookingItemRequest(
    Guid ServiceId,
    Guid? TherapistId,
    Guid? RoomId,
    TherapistSelectionMode TherapistSelectionMode = TherapistSelectionMode.Manual,
    int SortOrder = 0);

public record BookingItemData(
    Service Service,
    Guid? TherapistId,
    Guid? RoomId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int DurationMins,
    decimal Price,
    decimal? Commission,
    TherapistSelectionMode TherapistSelectionMode,
    int SortOrder);

public record BookingResult(bool Success, Guid? BookingId, string? BookingNo, string? Error)
{
    public static BookingResult Ok(Guid id, string no) => new(true, id, no, null);
    public static BookingResult Fail(string error) => new(false, null, null, error);
}
