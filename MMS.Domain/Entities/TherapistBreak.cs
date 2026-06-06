using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class TherapistBreak : TenantEntity
{
    public Guid TherapistId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string DaysOfWeek { get; set; } = "[]"; // JSON [1,2,3,4,5]
    public bool IsActive { get; set; } = true;

    public Therapist Therapist { get; set; } = null!;
}