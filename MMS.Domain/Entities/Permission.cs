using MMS.Domain.Common;

namespace MMS.Domain.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;      // BOOKING_CREATE
    public string GroupName { get; set; } = string.Empty; // Booking
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}