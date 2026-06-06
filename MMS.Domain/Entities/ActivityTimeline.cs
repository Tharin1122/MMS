using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class ActivityTimeline : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? EntityLabel { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Metadata { get; set; }
}