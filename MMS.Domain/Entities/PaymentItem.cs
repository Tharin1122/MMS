using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class PaymentItem : TenantEntity
{
    public Guid PaymentId { get; set; }
    public string RefType { get; set; } = string.Empty; // BookingItem | WalkInItem
    public Guid? RefId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? TherapistName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal DiscountAmount { get; set; } = 0;
    public decimal LineTotal { get; set; }
    public decimal? CommissionAmount { get; set; }

    public Payment Payment { get; set; } = null!;
}