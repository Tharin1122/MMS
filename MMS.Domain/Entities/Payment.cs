using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class Payment : TenantEntity
{
    public Guid BranchId { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public Guid? BookingId { get; set; }
    public Guid? WalkInId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; } = 0;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public Guid? CashierId { get; set; }
    public string? Notes { get; set; }

    public Branch Branch { get; set; } = null!;
    public Booking? Booking { get; set; }
    public WalkIn? WalkIn { get; set; }
    public Customer Customer { get; set; } = null!;
    public ICollection<PaymentItem> Items { get; set; } = [];
}