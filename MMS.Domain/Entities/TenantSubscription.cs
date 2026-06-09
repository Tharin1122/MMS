using MMS.Domain.Common;

namespace MMS.Domain.Entities;

/// <summary>
/// การ subscribe ของแต่ละ tenant — ดู plan ปัจจุบันจาก IsActive = true
/// </summary>
public class TenantSubscription : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }          // null = ตลอดชีพ (Free)
    public bool AutoRenew { get; set; } = false;
    public string? PaymentRef { get; set; }           // อ้างอิงการชำระ (อนาคต)
    public string? GatewayApiKey { get; set; }        // PromptPay key ที่ลูกค้าใส่เอง

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
}
