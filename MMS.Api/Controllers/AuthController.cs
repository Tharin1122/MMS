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
public class AuthController(
    AppDbContext db,
    JwtService jwtService,
    LineService lineService,
    PasswordService passwordService) : ControllerBase
{
    // ----------------------------------------------------------------
    // Username/Password Login (สำรอง — ใช้เมื่อเปลี่ยน LINE/มือถือ)
    // ----------------------------------------------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "กรุณากรอกชื่อผู้ใช้และรหัสผ่าน" });

        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u =>
                u.Username == req.Username && u.DeletedAt == null);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash)
            || !passwordService.Verify(user, user.PasswordHash, req.Password))
            return Unauthorized(new { message = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง" });

        if (!user.IsActive)
            return Unauthorized(new { message = "บัญชีนี้ถูกระงับการใช้งาน" });

        return Ok(await IssueTokensAsync(user));
    }

    // ----------------------------------------------------------------
    // ตั้ง/แก้ไข Username + Password + เบอร์โทร (ของตัวเอง)
    // ----------------------------------------------------------------
    [HttpPost("set-credentials")]
    [Authorize]
    public async Task<IActionResult> SetCredentials([FromBody] SetCredentialsRequest req)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
        if (user == null) return NotFound(new { message = "ไม่พบผู้ใช้" });

        if (!string.IsNullOrWhiteSpace(req.Username))
        {
            var taken = await db.Users.AnyAsync(u =>
                u.Username == req.Username && u.Id != userId && u.DeletedAt == null);
            if (taken) return BadRequest(new { message = "ชื่อผู้ใช้นี้ถูกใช้แล้ว" });
            user.Username = req.Username.Trim();
        }

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            if (req.Password.Length < 6)
                return BadRequest(new { message = "รหัสผ่านต้องยาวอย่างน้อย 6 ตัวอักษร" });
            user.PasswordHash = passwordService.Hash(user, req.Password);
        }

        if (req.Phone != null) user.Phone = req.Phone.Trim();
        if (req.DisplayName != null && req.DisplayName.Trim() != "")
            user.DisplayName = req.DisplayName.Trim();

        await db.SaveChangesAsync();
        return Ok(new { message = "บันทึกข้อมูลสำเร็จ" });
    }

    // helper — ออก access + refresh token (ใช้ร่วมทั้ง LINE และ password login)
    private async Task<object> IssueTokensAsync(User user)
    {
        user.LastLoginAt = DateTime.UtcNow;

        var deviceInfo = Request.Headers.UserAgent.ToString();
        var refreshToken = jwtService.GenerateRefreshToken(user.Id, deviceInfo);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        var permissions = user.UserRoles
            .Where(ur => ur.ExpiresAt == null || ur.ExpiresAt > DateTime.UtcNow)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var accessToken = jwtService.GenerateToken(user, permissions);

        return new
        {
            accessToken,
            refreshToken = refreshToken.Token,
            expiresIn = 60,
            user = new
            {
                id = user.Id,
                displayName = user.DisplayName,
                avatarUrl = user.AvatarUrl,
                tenantId = user.TenantId,
                branchId = user.BranchId,
            },
            permissions
        };
    }

    // ----------------------------------------------------------------
    // LINE Login (Production) — รับ LINE Access Token จาก LIFF
    // ----------------------------------------------------------------
    /// <summary>
    /// LINE Login จริง — ส่ง accessToken จาก LIFF มาให้ Server ยืนยันกับ LINE API
    /// </summary>
    [HttpPost("line-login")]
    public async Task<IActionResult> LineLogin([FromBody] LineLoginRequest req)
    {
        string lineUserId;
        string? displayName;
        string? avatarUrl;

        // --- Dev mode: ส่ง lineUserId ตรงๆ ได้ถ้าไม่มี ChannelId config ---
        if (!string.IsNullOrWhiteSpace(req.LineUserId))
        {
            lineUserId = req.LineUserId;
            displayName = req.DisplayName;
            avatarUrl = req.AvatarUrl;
        }
        // --- Production mode: ยืนยัน access token กับ LINE API ---
        else if (!string.IsNullOrWhiteSpace(req.AccessToken))
        {
            var verify = await lineService.VerifyAccessTokenAsync(req.AccessToken);
            if (!verify.Success)
                return Unauthorized(new { message = verify.ErrorMessage });

            lineUserId = verify.LineUserId!;
            displayName = verify.DisplayName;
            avatarUrl = verify.AvatarUrl;
        }
        else
        {
            return BadRequest(new { message = "ต้องส่ง lineUserId (dev) หรือ accessToken (production)" });
        }

        // หา User จาก LineUserId
        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.LineUserId == lineUserId && u.DeletedAt == null);

        if (user == null)
            return Unauthorized(new { message = "ไม่พบผู้ใช้นี้ในระบบ กรุณาติดต่อผู้ดูแล" });

        if (!user.IsActive)
            return Unauthorized(new { message = "บัญชีนี้ถูกระงับการใช้งาน" });

        // อัปเดต profile จาก LINE (ถ้ามีข้อมูลใหม่)
        if (!string.IsNullOrWhiteSpace(displayName) && user.DisplayName != displayName)
            user.DisplayName = displayName;
        if (!string.IsNullOrWhiteSpace(avatarUrl) && user.AvatarUrl != avatarUrl)
            user.AvatarUrl = avatarUrl;

        return Ok(await IssueTokensAsync(user));
    }

    // ----------------------------------------------------------------
    // Refresh Token — แลก refresh token เป็น access token ใหม่
    // ----------------------------------------------------------------
    /// <summary>
    /// แลก Refresh Token → Access Token ใหม่ (ไม่ต้อง login ซ้ำ)
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var stored = await db.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(rt => rt.Token == req.RefreshToken && rt.DeletedAt == null);

        if (stored == null || !stored.IsActive)
            return Unauthorized(new { message = "Refresh token ไม่ถูกต้องหรือหมดอายุ" });

        if (!stored.User.IsActive)
            return Unauthorized(new { message = "บัญชีนี้ถูกระงับการใช้งาน" });

        // Revoke token เก่า (Rotation)
        stored.IsRevoked = true;
        stored.RevokedReason = "rotated";

        // ออก token ใหม่
        var newRefresh = jwtService.GenerateRefreshToken(
            stored.UserId, Request.Headers.UserAgent.ToString());
        db.RefreshTokens.Add(newRefresh);

        await db.SaveChangesAsync();

        var permissions = stored.User.UserRoles
            .Where(ur => ur.ExpiresAt == null || ur.ExpiresAt > DateTime.UtcNow)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var accessToken = jwtService.GenerateToken(stored.User, permissions);

        return Ok(new
        {
            accessToken,
            refreshToken = newRefresh.Token,
            expiresIn = 60
        });
    }

    // ----------------------------------------------------------------
    // Logout — Revoke refresh token
    // ----------------------------------------------------------------
    /// <summary>
    /// Logout — ยกเลิก Refresh Token
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();

        // Revoke refresh tokens ทั้งหมดของ user นี้
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.DeletedAt == null)
            .ToListAsync();

        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedReason = "logout";
        }

        await db.SaveChangesAsync();

        return Ok(new { message = "Logout สำเร็จ" });
    }

    // ----------------------------------------------------------------
    // Me
    // ----------------------------------------------------------------
    /// <summary>
    /// ดูข้อมูลตัวเองจาก JWT
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
    /// ทดสอบ RBAC — เฉพาะคนที่มีสิทธิ์ SETTINGS_EDIT
    /// </summary>
    [HttpGet("test-permission")]
    [Authorize]
    [RequirePermission(PermissionCodes.SettingsEdit)]
    public IActionResult TestPermission()
    {
        return Ok(new { message = $"✅ คุณมีสิทธิ์ {PermissionCodes.SettingsEdit}" });
    }

    // ----------------------------------------------------------------
    // Seed (Dev only)
    // ----------------------------------------------------------------
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        await DbSeeder.SeedAsync(db);
        return Ok(new { message = "Seed สำเร็จ" });
    }
}

public record LineLoginRequest(
    string? LineUserId,      // Dev: ส่ง LineUserId ตรงๆ
    string? AccessToken,     // Production: ส่ง LINE Access Token จาก LIFF
    string? DisplayName,
    string? AvatarUrl);

public record RefreshRequest(string RefreshToken);

public record LoginRequest(string Username, string Password);

public record SetCredentialsRequest(
    string? Username,
    string? Password,
    string? Phone,
    string? DisplayName);
