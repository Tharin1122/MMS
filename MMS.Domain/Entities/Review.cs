using MMS.Domain.Common;

namespace MMS.Domain.Entities;

/// <summary>
/// รีวิว/คะแนนจากลูกค้า — ให้คะแนนหมอนวด/ร้าน หลังใช้บริการ
/// </summary>
public class Review : TenantEntity
{
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? TherapistId { get; set; }
    public Guid? PaymentId { get; set; }
    public int Rating { get; set; }            // 1-5 ดาว
    public string? Comment { get; set; }

    public Customer Customer { get; set; } = null!;
    public Therapist? Therapist { get; set; }
}
