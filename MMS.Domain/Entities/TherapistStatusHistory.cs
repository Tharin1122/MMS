using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class TherapistStatusHistory : TenantEntity
{
    public Guid TherapistId { get; set; }
    public TherapistStatus? FromStatus { get; set; }
    public TherapistStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public Guid? BookingId { get; set; }

    public Therapist Therapist { get; set; } = null!;
}