using MMS.Domain.Entities;
using MMS.Domain.Enums;

namespace MMS.Infrastructure.Persistence.Services;

/// <summary>
/// Helper สำหรับ Controller/Service อื่นใช้ — Queue notification ลง DB
/// Hangfire จะมาส่งทีหลัง
/// </summary>
public class NotificationService(AppDbContext db)
{
    /// <summary>
    /// Queue LINE message ให้ลูกค้า
    /// </summary>
    public async Task QueueLineAsync(
        string lineUserId,
        string message,
        string eventType,
        Guid? tenantId = null,
        string? entityType = null,
        Guid? entityId = null,
        DateTime? scheduledAt = null)
    {
        db.NotificationQueues.Add(new NotificationQueue
        {
            TenantId = tenantId,
            RecipientType = "Customer",
            LineUserId = lineUserId,
            Channel = NotificationChannel.Line,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Message = message,
            Status = NotificationStatus.Pending,
            ScheduledAt = scheduledAt ?? DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Queue LINE message ให้ Therapist
    /// </summary>
    public async Task QueueLineToTherapistAsync(
        string lineUserId,
        string message,
        string eventType,
        Guid? tenantId = null)
    {
        db.NotificationQueues.Add(new NotificationQueue
        {
            TenantId = tenantId,
            RecipientType = "Therapist",
            LineUserId = lineUserId,
            Channel = NotificationChannel.Line,
            EventType = eventType,
            Message = message,
            Status = NotificationStatus.Pending,
            ScheduledAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }
}