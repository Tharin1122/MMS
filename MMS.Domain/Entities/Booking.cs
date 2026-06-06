using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class Booking : TenantEntity
{
    public Guid BranchId { get; set; }
    public string BookingNo { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int TotalDurationMins { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; } = 0;
    public DepositStatus DepositStatus { get; set; } = DepositStatus.None;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public BookingSource Source { get; set; } = BookingSource.Staff;
    public string? Notes { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public Branch Branch { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<BookingItem> Items { get; set; } = [];
    public Payment? Payment { get; set; }
}