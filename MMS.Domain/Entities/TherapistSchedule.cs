using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class TherapistSchedule : TenantEntity
{
    public Guid TherapistId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsWorkday { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public Therapist Therapist { get; set; } = null!;
}