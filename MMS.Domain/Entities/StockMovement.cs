using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class StockMovement : TenantEntity
{
    public Guid InventoryItemId { get; set; }
    public string Type { get; set; } = "in";   // in (รับเข้า) / out (เบิกใช้)
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public DateTime MovedAt { get; set; } = DateTime.UtcNow;
    public InventoryItem? Item { get; set; }
}
