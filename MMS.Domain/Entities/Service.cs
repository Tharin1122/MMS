using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class Service : TenantEntity
{
    public Guid? BranchId { get; set; } // null = ทุกสาขา
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationMins { get; set; }
    public int BufferMins { get; set; } = 0;
    public decimal Price { get; set; }
    public string? RequiredRoomType { get; set; } // null = ทุกประเภท
    public decimal? CommissionRate { get; set; }
    public decimal? CommissionFixed { get; set; }
    public bool IsActive { get; set; } = true;

    public ServiceCategory Category { get; set; } = null!;
    public Branch? Branch { get; set; }
    public ICollection<TherapistService> TherapistServices { get; set; } = [];
}