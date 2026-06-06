using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class TherapistBlockTime : TenantEntity
{
    public Guid TherapistId { get; set; }
    public DateOnly BlockDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Reason { get; set; }

    public Therapist Therapist { get; set; } = null!;
}