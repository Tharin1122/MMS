using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class WalkIn : TenantEntity
{
    public Guid BranchId { get; set; }
    public string QueueNo { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public WalkInStatus Status { get; set; } = WalkInStatus.Waiting;
    public DateTime ArrivalTime { get; set; } = DateTime.UtcNow;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? EstimatedWaitMins { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Notes { get; set; }

    public Branch Branch { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<WalkInItem> Items { get; set; } = [];
    public Payment? Payment { get; set; }
}