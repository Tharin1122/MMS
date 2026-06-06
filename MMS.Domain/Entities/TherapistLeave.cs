using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class TherapistLeave : TenantEntity
{
    public Guid TherapistId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public LeaveType LeaveType { get; set; }
    public string? Reason { get; set; }
    public Guid? ApprovedBy { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public Therapist Therapist { get; set; } = null!;
}