using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class User : TenantEntity
{
    public Guid BranchId { get; set; }
    public string? LineUserId { get; set; }
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public AuthProvider AuthProvider { get; set; } = AuthProvider.Line;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public ICollection<UserRole> UserRoles { get; set; } = [];
}