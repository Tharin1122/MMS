using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class InventoryItem : TenantEntity
{
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = "";
    public string? Sku { get; set; }
    public string? Category { get; set; }   // น้ำมัน / สมุนไพร / ของใช้
    public string Unit { get; set; } = "ชิ้น";
    public decimal CostPerUnit { get; set; }
    public int Quantity { get; set; }
    public int ReorderPoint { get; set; }
}
