using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

/// <summary>
/// แผนการใช้งาน (Free / Basic / Shop / Business)
/// ข้อมูลนี้ admin กำหนด ไม่ได้ผูกกับ tenant
/// </summary>
public class SubscriptionPlan : BaseEntity
{
    public PlanCode Code { get; set; }
    public string Name { get; set; } = string.Empty;          // ชื่อแผน (ภาษาไทย)
    public string? Description { get; set; }
    public decimal PriceMonthly { get; set; }                 // ราคารายเดือน
    public decimal PriceYearly { get; set; }                  // ราคารายปี (discount)
    public bool IsActive { get; set; } = true;

    // Feature flags (int เพื่อ flexibility: -1 = ไม่จำกัด, 0 = ปิด, N = จำกัด N)
    public int MaxTherapists { get; set; } = 3;               // จำนวนหมอนวดสูงสุด
    public int MaxBranches { get; set; } = 1;                 // จำนวนสาขาสูงสุด
    public int MaxMonthlyWalkIns { get; set; } = 50;          // คิว walk-in/เดือน (-1=ไม่จำกัด)
    public bool HasLineNotify { get; set; } = false;          // แจ้งเตือน LINE OA
    public bool HasOnlineBooking { get; set; } = false;       // จองออนไลน์
    public bool HasTaxInvoice { get; set; } = false;          // ใบกำกับภาษีเต็ม
    public bool HasPaymentGateway { get; set; } = false;      // รับชำระ PromptPay
    public bool HasReportExport { get; set; } = false;        // export รายงาน CSV/Excel
    public bool HasApiAccess { get; set; } = false;           // API integration

    // Navigation
    public ICollection<TenantSubscription> Subscriptions { get; set; } = [];
}
