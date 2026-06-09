using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Infrastructure.Persistence;
using MMS.Infrastructure.Persistence.Auth;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController(AppDbContext db, PasswordService passwordService) : ControllerBase
{
    // GET /api/user/roles — บทบาทระบบที่เลือกได้ตอนสร้าง user
    [HttpGet("roles")]
    [RequirePermission(PermissionCodes.UserView)]
    public async Task<IActionResult> GetRoles()
    {
        var tenantId = User.GetTenantId();
        var roles = await db.Roles
            .Where(r => (r.TenantId == null || r.TenantId == tenantId)
                && !r.Name.StartsWith("custom_"))
            .Select(r => new { r.Id, r.Name, r.Description })
            .ToListAsync();
        return Ok(roles);
    }

    // POST /api/user — สร้างพนักงานใหม่
    [HttpPost]
    [RequirePermission(PermissionCodes.UserCreate)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { message = "กรุณากรอกชื่อพนักงาน" });

        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == req.RoleId
            && (r.TenantId == null || r.TenantId == tenantId));
        if (role == null) return BadRequest(new { message = "ไม่พบบทบาทที่เลือก" });

        // เฉพาะ Owner เท่านั้นที่สร้าง Owner คนอื่นได้
        var callerIsOwner = (await db.UserRoles
            .Where(ur => ur.UserId == User.GetUserId())
            .Select(ur => ur.Role.Name).ToListAsync()).Contains("Owner");
        if (role.Name == "Owner" && !callerIsOwner)
            return StatusCode(403, new { message = "เฉพาะเจ้าของร้านเท่านั้นที่เพิ่ม Owner ได้" });

        var user = new User
        {
            TenantId = tenantId,
            BranchId = branchId,
            DisplayName = req.DisplayName.Trim(),
            Phone = req.Phone?.Trim(),
            IsActive = true,
            UserRoles = new List<UserRole>
            {
                new() { RoleId = role.Id, BranchId = branchId, AssignedAt = DateTime.UtcNow }
            }
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new { user.Id, user.DisplayName, message = "เพิ่มพนักงานสำเร็จ" });
    }

    // PUT /api/user/{id} — แก้ชื่อ/เบอร์/บล็อก
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.UserEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req)
    {
        var (user, error) = await GetManageableUser(id);
        if (error != null) return error;

        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user!.DisplayName = req.DisplayName.Trim();
        if (req.Phone != null) user!.Phone = req.Phone.Trim();
        if (req.IsActive.HasValue)
        {
            if (id == User.GetUserId())
                return BadRequest(new { message = "ไม่สามารถบล็อกตัวเองได้" });
            user!.IsActive = req.IsActive.Value;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "อัปเดตข้อมูลสำเร็จ" });
    }

    // DELETE /api/user/{id} — ลบ (soft delete)
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.UserEdit)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (id == User.GetUserId())
            return BadRequest(new { message = "ไม่สามารถลบบัญชีตัวเองได้" });

        var (user, error) = await GetManageableUser(id);
        if (error != null) return error;

        user!.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "ลบพนักงานสำเร็จ" });
    }

    // POST /api/user/{id}/set-password — แอดมินตั้ง/ล้างรหัสผ่านชั่วคราว
    [HttpPost("{id:guid}/set-password")]
    [RequirePermission(PermissionCodes.UserEdit)]
    public async Task<IActionResult> SetPassword(Guid id, [FromBody] AdminSetPasswordRequest req)
    {
        var (user, error) = await GetManageableUser(id);
        if (error != null) return error;

        if (string.IsNullOrEmpty(req.NewPassword))
        {
            user!.PasswordHash = null;   // ล้างรหัส (ให้ผู้ใช้ตั้งใหม่เอง)
            await db.SaveChangesAsync();
            return Ok(new { message = "ล้างรหัสผ่านแล้ว ผู้ใช้ต้องตั้งรหัสใหม่" });
        }

        if (req.NewPassword.Length < 6)
            return BadRequest(new { message = "รหัสผ่านต้องยาวอย่างน้อย 6 ตัวอักษร" });

        user!.PasswordHash = passwordService.Hash(user, req.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "ตั้งรหัสผ่านชั่วคราวสำเร็จ" });
    }

    // helper — ดึง user ที่ caller มีสิทธิ์จัดการ (ห้ามแตะ Owner ถ้าตัวเองไม่ใช่ Owner)
    private async Task<(User?, IActionResult?)> GetManageableUser(Guid id)
    {
        var tenantId = User.GetTenantId();
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId && u.DeletedAt == null);

        if (user == null) return (null, NotFound(new { message = "ไม่พบผู้ใช้" }));

        var callerIsOwner = (await db.UserRoles
            .Where(ur => ur.UserId == User.GetUserId())
            .Select(ur => ur.Role.Name).ToListAsync()).Contains("Owner");
        var targetIsOwner = user.UserRoles.Any(ur => ur.Role.Name == "Owner");

        if (targetIsOwner && !callerIsOwner && id != User.GetUserId())
            return (null, StatusCode(403, new { message = "ไม่สามารถจัดการบัญชี Owner ได้" }));

        return (user, null);
    }

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
                u.Phone,
                u.Username,
                hasLine = u.LineUserId != null,
                hasPassword = u.PasswordHash != null,
                roles = u.UserRoles.Select(ur => ur.Role.Name).Where(n => !n.StartsWith("custom_")),
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

        var targetUser = await db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId && u.DeletedAt == null);
        if (targetUser == null) return NotFound(new { message = "ไม่พบผู้ใช้" });

        // dedupe ตาม Code (กันกรณีมี permission ซ้ำใน DB)
        var allPermissions = (await db.Permissions.ToListAsync())
            .GroupBy(p => p.Code)
            .Select(g => g.First())
            .ToList();

        var userPermCodes = await db.UserRoles
            .Where(ur => ur.UserId == id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        var callerPermCodes = User.GetPermissions();

        var groups = allPermissions
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

        return Ok(new { userName = targetUser.DisplayName, groups });
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

        var perms = (await db.Permissions
            .Where(p => req.PermissionCodes.Contains(p.Code))
            .ToListAsync())
            .GroupBy(p => p.Code)
            .Select(g => g.First())   // dedupe ตาม code
            .ToList();

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

public record CreateUserRequest(string DisplayName, string? Phone, Guid RoleId);

public record UpdateUserRequest(string? DisplayName, string? Phone, bool? IsActive);

public record AdminSetPasswordRequest(string? NewPassword);