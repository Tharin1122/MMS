using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;
using MMS.Infrastructure.Persistence.Auth;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, JwtService jwtService) : ControllerBase
{
    /// <summary>
    /// LINE Login — รับ LineUserId จาก LIFF แล้วคืน JWT
    /// </summary>
    [HttpPost("line-login")]
    public async Task<IActionResult> LineLogin([FromBody] LineLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LineUserId))
            return BadRequest(new { message = "LineUserId is required" });

        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.LineUserId == req.LineUserId && u.DeletedAt == null);

        if (user == null)
            return Unauthorized(new { message = "ไม่พบผู้ใช้นี้ในระบบ กรุณาติดต่อผู้ดูแล" });

        if (!user.IsActive)
            return Unauthorized(new { message = "บัญชีนี้ถูกระงับการใช้งาน" });

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var permissions = user.UserRoles
            .Where(ur => ur.ExpiresAt == null || ur.ExpiresAt > DateTime.UtcNow)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var token = jwtService.GenerateToken(user, permissions);

        return Ok(new
        {
            token,
            user = new
            {
                id = user.Id,
                displayName = user.DisplayName,
                avatarUrl = user.AvatarUrl,
                tenantId = user.TenantId,
                branchId = user.BranchId,
            },
            permissions
        });
    }

    /// <summary>
    /// ดูข้อมูลตัวเองจาก JWT (ต้อง login ก่อน)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.GetUserId(),
            tenantId = User.GetTenantId(),
            branchId = User.GetBranchId(),
            displayName = User.GetDisplayName(),
            permissions = User.GetPermissions()
        });
    }

    /// <summary>
    /// ทดสอบ RBAC — เฉพาะคนที่มีสิทธิ์ SETTINGS_EDIT เท่านั้น
    /// </summary>
    [HttpGet("test-permission")]
    [Authorize]
    [RequirePermission(PermissionCodes.SettingsEdit)]
    public IActionResult TestPermission()
    {
        return Ok(new { message = $"✅ คุณมีสิทธิ์ {PermissionCodes.SettingsEdit}" });
    }

    /// <summary>
    /// Seed ข้อมูลตัวอย่าง (Dev only)
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        await DbSeeder.SeedAsync(db);
        return Ok(new { message = "Seed สำเร็จ" });
    }
}

public record LineLoginRequest(string LineUserId, string? DisplayName, string? AvatarUrl);
