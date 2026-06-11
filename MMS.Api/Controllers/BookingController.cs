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
public class BookingController(
    AppDbContext db,
    BookingService bookingService,
    IRealtimeService realtime,
    ActivityTimelineService timeline) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.BookingView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateOnly? date,
        [FromQuery] BookingStatus? status,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? therapistId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var query = db.Bookings
            .Where(b => b.TenantId == tenantId && b.BranchId == branchId && b.DeletedAt == null);

        if (date.HasValue) query = query.Where(b => b.BookingDate == date);
        if (status.HasValue) query = query.Where(b => b.Status == status);
        if (customerId.HasValue) query = query.Where(b => b.CustomerId == customerId);
        if (therapistId.HasValue)
            query = query.Where(b => b.Items.Any(i => i.TherapistId == therapistId));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.BookingDate).ThenBy(b => b.StartTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.BookingNo,
                b.BookingDate,
                b.StartTime,
                b.EndTime,
                b.TotalDurationMins,
                b.TotalAmount,
                b.Status,
                b.Source,
                Customer = new { b.Customer.Id, b.Customer.DisplayName, b.Customer.Phone },
                ItemCount = b.Items.Count
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.BookingView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var booking = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Items).ThenInclude(i => i.Service)
            .Include(b => b.Items).ThenInclude(i => i.Therapist)
            .Include(b => b.Items).ThenInclude(i => i.Room)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId && b.DeletedAt == null);

        if (booking == null) return NotFound(new { message = "Booking not found" });

        return Ok(new
        {
            booking.Id,
            booking.BookingNo,
            booking.BookingDate,
            booking.StartTime,
            booking.EndTime,
            booking.TotalDurationMins,
            booking.TotalAmount,
            booking.DepositAmount,
            booking.DepositStatus,
            booking.Status,
            booking.Source,
            booking.Notes,
            booking.CancelReason,
            booking.CancelledAt,
            Customer = new { booking.Customer.Id, booking.Customer.DisplayName, booking.Customer.Phone },
            Items = booking.Items.OrderBy(i => i.SortOrder).Select(i => new
            {
                i.Id,
                i.SortOrder,
                i.StartTime,
                i.EndTime,
                i.DurationMins,
                i.Price,
                i.CommissionAmount,
                Service = new { i.Service.Id, i.Service.Name },
                Therapist = i.Therapist == null ? null : new { i.Therapist.Id, i.Therapist.DisplayName },
                Room = i.Room == null ? null : new { i.Room.Id, i.Room.Name }
            })
        });
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.BookingCreate)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "Booking must have at least 1 item" });

        if (req.BookingDate < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            return BadRequest(new { message = "Cannot book in the past" });

        var result = await bookingService.CreateBookingAsync(tenantId, branchId, User.GetUserId(), req);
        if (!result.Success) return BadRequest(new { message = result.Error });

        // 🔔 Realtime
        var customer = await db.Customers.FindAsync(req.CustomerId);
        await realtime.NotifyBookingUpdatedAsync(
            branchId, result.BookingId!.Value, result.BookingNo!,
            "Pending", customer?.DisplayName ?? "");

        await timeline.LogAsync("booking_created", "Booking", result.BookingId!.Value,
            $"สร้างการจอง {result.BookingNo} · {customer?.DisplayName}", result.BookingNo);

        return Ok(new { message = "Booking created", bookingId = result.BookingId, bookingNo = result.BookingNo });
    }

    [HttpPatch("{id:guid}/confirm")]
    [RequirePermission(PermissionCodes.BookingEdit)]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await bookingService.ConfirmAsync(id, tenantId);
        if (!ok) return BadRequest(new { message = err });

        var booking = await db.Bookings.Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == id);
        await realtime.NotifyBookingUpdatedAsync(
            branchId, id, booking?.BookingNo ?? "", "Confirmed", booking?.Customer.DisplayName ?? "");

        return Ok(new { message = "Booking confirmed" });
    }

    [HttpPatch("{id:guid}/start")]
    [RequirePermission(PermissionCodes.BookingEdit)]
    public async Task<IActionResult> Start(Guid id)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await bookingService.StartAsync(id, tenantId);
        if (!ok) return BadRequest(new { message = err });

        var booking = await db.Bookings.Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == id);
        await realtime.NotifyBookingUpdatedAsync(
            branchId, id, booking?.BookingNo ?? "", "InProgress", booking?.Customer.DisplayName ?? "");

        return Ok(new { message = "Booking started" });
    }

    [HttpPatch("{id:guid}/complete")]
    [RequirePermission(PermissionCodes.BookingEdit)]
    public async Task<IActionResult> Complete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await bookingService.CompleteAsync(id, tenantId);
        if (!ok) return BadRequest(new { message = err });

        var booking = await db.Bookings.Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == id);
        await realtime.NotifyBookingUpdatedAsync(
            branchId, id, booking?.BookingNo ?? "", "Completed", booking?.Customer.DisplayName ?? "");

        return Ok(new { message = "Booking completed" });
    }

    [HttpPatch("{id:guid}/cancel")]
    [RequirePermission(PermissionCodes.BookingCancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var (ok, err) = await bookingService.CancelAsync(id, tenantId, User.GetUserId(), req.Reason);
        if (!ok) return BadRequest(new { message = err });

        var booking = await db.Bookings.Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == id);
        await realtime.NotifyBookingUpdatedAsync(
            branchId, id, booking?.BookingNo ?? "", "Cancelled", booking?.Customer.DisplayName ?? "");

        return Ok(new { message = "Booking cancelled" });
    }

    [HttpPatch("{id:guid}/no-show")]
    [RequirePermission(PermissionCodes.BookingEdit)]
    public async Task<IActionResult> NoShow(Guid id)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var booking = await db.Bookings.Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId && b.DeletedAt == null);

        if (booking == null) return NotFound(new { message = "Booking not found" });
        if (booking.Status != BookingStatus.Confirmed)
            return BadRequest(new { message = $"Cannot mark no-show with status {booking.Status}" });

        booking.Status = BookingStatus.NoShow;
        await db.SaveChangesAsync();

        await realtime.NotifyBookingUpdatedAsync(
            branchId, id, booking.BookingNo, "NoShow", booking.Customer.DisplayName);

        return Ok(new { message = "Marked as no-show" });
    }

    [HttpPatch("{id:guid}/notes")]
    [RequirePermission(PermissionCodes.BookingEdit)]
    public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] NotesRequest req)
    {
        var tenantId = User.GetTenantId();
        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId && b.DeletedAt == null);

        if (booking == null) return NotFound(new { message = "Booking not found" });
        booking.Notes = req.Notes;
        await db.SaveChangesAsync();
        return Ok(new { message = "Notes updated" });
    }

    [HttpPatch("{id:guid}/deposit")]
    [RequirePermission(PermissionCodes.BookingEdit)]
    public async Task<IActionResult> UpdateDeposit(Guid id, [FromBody] DepositRequest req)
    {
        var tenantId = User.GetTenantId();
        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId && b.DeletedAt == null);

        if (booking == null) return NotFound(new { message = "Booking not found" });
        booking.DepositAmount = req.Amount;
        booking.DepositStatus = req.Status;
        await db.SaveChangesAsync();
        return Ok(new { message = "Deposit updated" });
    }
}

public record CancelRequest(string? Reason);
public record NotesRequest(string? Notes);
public record DepositRequest(decimal Amount, DepositStatus Status);
