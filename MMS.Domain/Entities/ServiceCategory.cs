using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class ServiceCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ICollection<Service> Services { get; set; } = [];
}