using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class Role : BaseEntity
{
    public Guid? TenantId { get; set; } // null = System role
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = false;

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}