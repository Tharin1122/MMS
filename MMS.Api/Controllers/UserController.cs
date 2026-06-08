using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// GET /api/user — ดู user ทั้งหมดใน branch
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.UserView)]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var users = await db.Users
            .Where(u => u.TenantId == tenantId
                && u.BranchId == branchId
                && u.DeletedAt == null)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.AvatarUrl,
                u.IsActive,
                u.LastLoginAt,
                roles = u.UserRoles.Select(ur => ur.Role.Name),
                permissionCount = u.UserRoles
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Select(rp => rp.PermissionId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// GET /api/user/{id}/permissions — ดู permission ของ user
    /// </summary>
    [HttpGet("{id:guid}/permissions")]
    [RequirePermission(PermissionCodes.UserView)]
    public async Task<IActionResult> GetUserPermissions(Guid id)
    {
        var tenantId = User.GetTenantId();

        var allPermissions = await db.Permissions.ToListAsync();

        var userPermCodes = await db.UserRoles
            .Where(ur => ur.UserId == id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        var callerPermCodes = User.GetPermissions();

        var result = allPermissions
            .GroupBy(p => p.GroupName)
            .Select(g => new
            {
                group = g.Key,
                permissions = g.Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Description,
                    userHas = userPermCodes.Contains(p.Code),
                    callerHas = callerPermCodes.Contains(p.Code),
                })
            });

        return Ok(result);
    }

    /// <summary>
    /// PUT /api/user/{id}/permissions — แก้ permission (caller ให้ได้เฉพาะสิทธิ์ที่ตัวเองมี)
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    [RequirePermission(PermissionCodes.UserRoleAssign)]
    public async Task<IActionResult> UpdateUserPermissions(
        Guid id, [FromBody] UpdatePermissionsRequest req)
    {
        var tenantId = User.GetTenantId();
        var callerId = User.GetUserId();
        var callerPermCodes = User.GetPermissions().ToHashSet();

        // ห้ามแก้สิทธิ์ตัวเอง
        if (callerId == id)
            return StatusCode(403, new { message = "ไม่สามารถแก้สิทธิ์ของตัวเองได้" });

        // ห้ามให้สิทธิ์ที่ตัวเองไม่มี
        var forbidden = req.PermissionCodes
            .Where(code => !callerPermCodes.Contains(code))
            .ToList();

        if (forbidden.Any())
            return StatusCode(403, new { message = "ไม่สามารถให้สิทธิ์ที่ตัวเองไม่มี", forbidden });

        // ห้ามแก้สิทธิ์คนที่เป็น Owner (ถ้า caller ไม่ใช่ Owner)
        var callerRoles = await db.UserRoles
            .Where(ur => ur.UserId == callerId)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        var targetRoles = await db.UserRoles
            .Where(ur => ur.UserId == id)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        var callerIsOwner = callerRoles.Any(r => r == "Owner");
        var targetIsOwner = targetRoles.Any(r => r == "Owner");

        if (!callerIsOwner && targetIsOwner)
            return StatusCode(403, new { message = "ไม่สามารถแก้ไขสิทธิ์ของ Owner ได้" });

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id
                && u.TenantId == tenantId
                && u.DeletedAt == null);

        if (user == null) return NotFound(new { message = "ไม่พบผู้ใช้" });

        var customRoleName = $"custom_{id}";
        var customRole = await db.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId
                && r.Name == customRoleName);

        if (customRole == null)
        {
            customRole = new Role
            {
                TenantId = tenantId,
                Name = customRoleName,
                Description = "Custom permissions",
                IsSystem = false,
            };
            db.Roles.Add(customRole);
            await db.SaveChangesAsync();
        }

        var hasRole = await db.UserRoles
            .AnyAsync(ur => ur.UserId == id && ur.RoleId == customRole.Id);

        if (!hasRole)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = id,
                RoleId = customRole.Id,
                BranchId = user.BranchId,
                AssignedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var perms = await db.Permissions
            .Where(p => req.PermissionCodes.Contains(p.Code))
            .ToListAsync();

        var oldPerms = await db.RolePermissions
            .Where(rp => rp.RoleId == customRole.Id)
            .ToListAsync();

        db.RemoveRange(oldPerms);
        await db.SaveChangesAsync();

        var newPerms = perms.Select(p => new RolePermission
        {
            RoleId = customRole.Id,
            PermissionId = p.Id
        }).ToList();

        await db.AddRangeAsync(newPerms);
        await db.SaveChangesAsync();

        return Ok(new { message = "อัพเดทสิทธิ์สำเร็จ" });
    }
}

public record UpdatePermissionsRequest(List<string> PermissionCodes);