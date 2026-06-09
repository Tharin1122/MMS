using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class OtpToken : BaseEntity
{
    public string UserId { get; set; } = string.Empty;  // เก็บ username หรือ email สำหรับ reset
    public string Code { get; set; } = string.Empty;     // OTP 6 หลัก
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; } = 0;               // จำนวนครั้งกรอกผิด
    public bool IsUsed { get; set; } = false;
}
