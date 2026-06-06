using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class NotificationQueue : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string RecipientType { get; set; } = string.Empty;
    public Guid? RecipientId { get; set; }
    public string? LineUserId { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.Line;
    public string EventType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Message { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
}