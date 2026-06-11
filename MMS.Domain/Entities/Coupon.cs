using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class Coupon : TenantEntity
{
    public string Code { get; set; } = "";
    public string? Campaign { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public DateOnly? ExpiresAt { get; set; }
    public int UsedCount { get; set; }
    public int Quota { get; set; }
    public bool IsActive { get; set; } = true;
}
