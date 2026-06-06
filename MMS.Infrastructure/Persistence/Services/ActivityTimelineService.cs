using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence.Services;

/// <summary>
/// Service สำหรับบันทึก ActivityTimeline แบบ manual
/// ใช้เมื่อต้องการ log event ที่มี description อธิบายได้ชัดเจน
/// เช่น "มิ้นท์ เปลี่ยนสถานะเป็น กำลังบริการ"
/// </summary>
public class ActivityTimelineService(
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly HttpContext? _http = httpContextAccessor.HttpContext;

    public async Task LogAsync(
        string eventType,
        string entityType,
        Guid entityId,
        string description,
        string? entityLabel = null,
        object? metadata = null,
        Guid? tenantId = null,
        Guid? branchId = null)
    {
        var actorId = GetUserId();
        var actorName = _http?.User.FindFirstValue(ClaimTypes.Name);

        var entry = new ActivityTimeline
        {
            TenantId = tenantId ?? GetTenantId(),
            BranchId = branchId ?? GetBranchId(),
            ActorId = actorId,
            ActorName = actorName,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            EntityLabel = entityLabel,
            Description = description,
            Metadata = metadata != null
                ? JsonSerializer.Serialize(metadata)
                : null
        };

        db.ActivityTimelines.Add(entry);
        await db.SaveChangesAsync();
    }

    private Guid? GetUserId()
    {
        var val = _http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(val, out var id) ? id : null;
    }

    private Guid? GetTenantId()
    {
        var val = _http?.User.FindFirstValue("tenantId");
        return Guid.TryParse(val, out var id) ? id : null;
    }

    private Guid? GetBranchId()
    {
        var val = _http?.User.FindFirstValue("branchId");
        return Guid.TryParse(val, out var id) ? id : null;
    }
}

/// <summary>
/// Event type constants — ใช้แทน string เพื่อป้องกัน typo
/// </summary>
public static class TimelineEvents
{
    // Auth
    public const string UserLogin = "USER_LOGIN";
    public const string UserLogout = "USER_LOGOUT";

    // Booking
    public const string BookingCreated = "BOOKING_CREATED";
    public const string BookingConfirmed = "BOOKING_CONFIRMED";
    public const string BookingCancelled = "BOOKING_CANCELLED";
    public const string BookingCompleted = "BOOKING_COMPLETED";

    // WalkIn
    public const string WalkInCreated = "WALKIN_CREATED";
    public const string WalkInStarted = "WALKIN_STARTED";
    public const string WalkInCompleted = "WALKIN_COMPLETED";

    // Therapist
    public const string TherapistStatusChanged = "THERAPIST_STATUS_CHANGED";
    public const string TherapistLeaveApproved = "THERAPIST_LEAVE_APPROVED";

    // Payment
    public const string PaymentCreated = "PAYMENT_CREATED";
    public const string PaymentRefunded = "PAYMENT_REFUNDED";
}
