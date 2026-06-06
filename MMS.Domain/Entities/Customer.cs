using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class Customer : TenantEntity
{
    public Guid BranchId { get; set; }
    public string? LineUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Notes { get; set; }
    public Guid? PreferredTherapistId { get; set; }
    public int TotalVisits { get; set; } = 0;
    public decimal TotalSpent { get; set; } = 0;
    public DateTime? LastVisitAt { get; set; }

    public Branch Branch { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<WalkIn> WalkIns { get; set; } = [];
}