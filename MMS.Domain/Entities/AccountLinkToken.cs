using MMS.Domain.Common;

namespace MMS.Domain.Entities;

/// <summary>
/// โทเคนผูกบัญชี LINE — แอดมินสร้างให้พนักงาน พนักงานสแกน QR ด้วย LINE เพื่อผูก
/// </summary>
public class AccountLinkToken : TenantEntity
{
    public Guid TargetUserId { get; set; }       // user ที่จะถูกผูก LINE
    public string Token { get; set; } = string.Empty;  // โค้ดสุ่มฝังใน QR
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? LinkedLineUserId { get; set; }      // LINE userId ที่ผูกสำเร็จ

    public User TargetUser { get; set; } = null!;
}
