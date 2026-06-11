using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class Promotion : TenantEntity
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Code { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public DateOnly? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int SoldCount { get; set; }
}
