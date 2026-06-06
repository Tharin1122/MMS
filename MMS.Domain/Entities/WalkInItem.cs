using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class WalkInItem : TenantEntity
{
    public Guid WalkInId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid? TherapistId { get; set; }
    public Guid? RoomId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMins { get; set; }
    public decimal Price { get; set; }
    public decimal? CommissionAmount { get; set; }
    public int SortOrder { get; set; } = 0;

    public WalkIn WalkIn { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Therapist? Therapist { get; set; }
    public Room? Room { get; set; }
}