using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class TherapistService : TenantEntity
{
    public Guid TherapistId { get; set; }
    public Guid ServiceId { get; set; }
    public bool IsActive { get; set; } = true;

    public Therapist Therapist { get; set; } = null!;
    public Service Service { get; set; } = null!;
}