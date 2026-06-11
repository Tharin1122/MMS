using MMS.Domain.Common;

namespace MMS.Domain.Entities;

/// <summary>
/// คอร์สที่ลูกค้าซื้อแล้ว — เก็บจำนวนครั้งคงเหลือ + วันหมดอายุ
/// </summary>
public class CustomerPackage : TenantEntity
{
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ServicePackageId { get; set; }
    public string PackageName { get; set; } = "";    // snapshot ชื่อคอร์สตอนซื้อ
    public int TotalSessions { get; set; }
    public int RemainingSessions { get; set; }
    public decimal PricePaid { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public Guid? PaymentId { get; set; }

    public Customer Customer { get; set; } = null!;
    public ServicePackage Package { get; set; } = null!;
}
