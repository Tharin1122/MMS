using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class Expense : TenantEntity
{
    public Guid? BranchId { get; set; }
    public string Category { get; set; } = "";   // ค่ามือ / ของใช้ / ค่าเช่า / การตลาด
    public string? Note { get; set; }
    public decimal Amount { get; set; }
    public DateTime SpentAt { get; set; } = DateTime.UtcNow;
}
