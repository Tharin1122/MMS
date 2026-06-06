using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class Therapist : TenantEntity
{
    public Guid BranchId { get; set; }
    public Guid? UserId { get; set; }
    public string? Code { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? LineUserId { get; set; }
    public string? AvatarUrl { get; set; }
    public int? ExperienceYears { get; set; }
    public SkillLevel? SkillLevel { get; set; }
    public TherapistStatus CurrentStatus { get; set; } = TherapistStatus.Offline;
    public Guid? CurrentBookingId { get; set; }
    public bool IsActive { get; set; } = true;

    public Branch Branch { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<TherapistStatusHistory> StatusHistories { get; set; } = [];
    public ICollection<TherapistSchedule> Schedules { get; set; } = [];
    public ICollection<TherapistLeave> Leaves { get; set; } = [];
    public ICollection<TherapistBreak> Breaks { get; set; } = [];
    public ICollection<TherapistBlockTime> BlockTimes { get; set; } = [];
    public ICollection<TherapistService> TherapistServices { get; set; } = [];
}