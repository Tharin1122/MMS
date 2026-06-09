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
    PasswordService passwordService,
    LineOtpService lineOtpService,
    IConfiguration config) : ControllerBase
{
    // ----------------------------------------------------------------
    // QR Account Linking — แอดมินสร้างโทเคนผูก LINE ให้พนักงาน
    // ----------------------------------------------------------------
    [HttpPost("link-token")]
    [Authorize]
    public async Task<IActionResult> CreateLinkToken([FromBody] CreateLinkTokenRequest req)
    {
        var tenantId = User.GetTenantId();
        var targetUser = await db.Users
            .FirstOrDefaultAsync(u => u.Id == req.UserId && u.TenantId == tenantId && u.DeletedAt == null);

        if (targetUser == null)
            return NotFound(new { message = "ไม่พบพนักงานนี้" });

        // ลบ token เก่าที่ยังไม่ใช้ของ user นี้
        var oldTokens = await db.AccountLinkTokens
            .Where(t => t.TargetUserId == req.UserId && t.UsedAt == null)
            .ToListAsync();
        db.AccountLinkTokens.RemoveRange(oldTokens);

        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..16];
        var linkToken = new AccountLinkToken
        {
            TenantId = targetUser.TenantId,
            TargetUserId = req.UserId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        };
        db.AccountLinkTokens.Add(linkToken);
        await db.SaveChangesAsync();

        var liffId = config["Line:LiffId"] ?? "";
        var liffUrl = $"https://liff.line.me/{liffId}?link={token}";

        return Ok(new
        {
            token,
            liffUrl,
            targetName = targetUser.DisplayName,
            expiresAt = linkToken.ExpiresAt,
        });
    }

    // ดูข้อมูล token (สำหรับหน้าผูกบัญชีก่อนยืนยัน)
    [HttpGet("link-info/{token}")]
    public async Task<IActionResult> GetLinkInfo(string token)
    {
        var linkToken = await db.AccountLinkTokens
            .Include(t => t.TargetUser)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (linkToken == null || linkToken.UsedAt != null || linkToken.ExpiresAt < DateTime.UtcNow)
            return NotFound(new { message = "ลิงก์ผูกบัญชีหมดอายุหรือถูกใช้แล้ว" });

        return Ok(new { targetName = linkToken.TargetUser.DisplayName });
    }

    // พนักงานสแกน QR → LINE login → ผูก LINE เข้ากับ user + login เข้าระบบ
    [HttpPost("link-line")]
    public async Task<IActionResult> LinkLine([FromBody] LinkLineRequest req)
    {
        var linkToken = await db.AccountLinkTokens
            .Include(t => t.TargetUser)
            .FirstOrDefaultAsync(t => t.Token == req.LinkToken);

        if (linkToken == null || linkToken.UsedAt != null || linkToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "ลิงก์ผูกบัญชีหมดอายุหรือถูกใช้แล้ว" });

        // verify LINE access token → ได้ lineUserId จริง
        var verify = await lineService.VerifyAccessTokenAsync(req.AccessToken);
        if (!verify.Success)
            return Unauthorized(new { message = verify.ErrorMessage });

        var lineUserId = verify.LineUserId!;

        // เช็คว่า LINE นี้ถูกผูกกับ user อื่นแล้วหรือยัง
        var alreadyLinked = await db.Users.AnyAsync(u =>
            u.LineUserId == lineUserId && u.Id != linkToken.TargetUserId && u.DeletedAt == null);
        if (alreadyLinked)
            return BadRequest(new { message = "LINE นี้ถูกผูกกับบัญชีอื่นแล้ว" });

        // ผูก LINE เข้ากับ user
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(u => u.Id == linkToken.TargetUserId);

        user.LineUserId = lineUserId;
        if (!string.IsNullOrWhiteSpace(verify.DisplayName) && string.IsNullOrWhiteSpace(user.DisplayName))
            user.DisplayName = verify.DisplayName;
        if (!string.IsNullOrWhiteSpace(verify.AvatarUrl))
            user.AvatarUrl = verify.AvatarUrl;

        linkToken.UsedAt = DateTime.UtcNow;
        linkToken.LinkedLineUserId = lineUserId;

        return Ok(await IssueTokensAsync(user));
    }

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
                username = user.Username,
                hasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                hasLine = !string.IsNullOrEmpty(user.LineUserId),
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
    // Request OTP for Password Reset
    // ----------------------------------------------------------------
    [HttpPost("request-reset-otp")]
    public async Task<IActionResult> RequestResetOtp([FromBody] RequestResetOtpRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest(new { message = "กรุณากรอกชื่อผู้ใช้" });

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.DeletedAt == null);

        if (user == null)
            return NotFound(new { message = "ไม่พบผู้ใช้นี้" });

        if (string.IsNullOrEmpty(user.LineUserId))
            return BadRequest(new { message = "บัญชีนี้ยังไม่ได้ผูก LINE โปรดติดต่อผู้ดูแล" });

        // ส่ง OTP
        var otpResult = await lineOtpService.SendOtpAsync(user.LineUserId);
        if (!otpResult.Success)
            return BadRequest(new { message = otpResult.ErrorMessage });

        // เก็บ OTP ไว้ database
        var existingOtp = await db.OtpTokens
            .Where(o => o.UserId == user.Id.ToString() && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();

        if (existingOtp != null)
            db.OtpTokens.Remove(existingOtp);

        var otpToken = new OtpToken
        {
            UserId = user.Id.ToString(),
            Code = otpResult.Otp!,
            ExpiresAt = otpResult.ExpiresAt!.Value,
            Attempts = 0
        };
        db.OtpTokens.Add(otpToken);
        await db.SaveChangesAsync();

        return Ok(new { message = "ส่ง OTP ไปยัง LINE แล้ว (ใช้ได้ 10 นาที)" });
    }

    // ----------------------------------------------------------------
    // Reset Password with OTP
    // ----------------------------------------------------------------
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Otp) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "กรุณากรอกข้อมูลให้ครบ" });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.DeletedAt == null);
        if (user == null)
            return NotFound(new { message = "ไม่พบผู้ใช้นี้" });

        // ตรวจสอบ OTP
        var otpToken = await db.OtpTokens
            .Where(o => o.UserId == user.Id.ToString() && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();

        if (otpToken == null || otpToken.Code != req.Otp.Trim())
        {
            if (otpToken != null)
            {
                otpToken.Attempts++;
                if (otpToken.Attempts >= 3)
                {
                    otpToken.IsUsed = true;  // Lock OTP after 3 attempts
                    await db.SaveChangesAsync();
                    return Unauthorized(new { message = "พยายามมากเกินไป OTP ถูกล็อก ขอใหม่เถิด" });
                }
                await db.SaveChangesAsync();
            }
            return Unauthorized(new { message = "OTP ไม่ถูกต้อง" });
        }

        // Reset password
        if (req.NewPassword.Length < 6)
            return BadRequest(new { message = "รหัสผ่านต้องยาวอย่างน้อย 6 ตัวอักษร" });

        user.PasswordHash = passwordService.Hash(user, req.NewPassword);
        otpToken.IsUsed = true;
        await db.SaveChangesAsync();

        return Ok(new { message = "รีเซ็ตรหัสผ่านสำเร็จ โปรดเข้าสู่ระบบใหม่" });
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

public record RequestResetOtpRequest(string Username);

public record ResetPasswordRequest(string Username, string Otp, string NewPassword);

public record CreateLinkTokenRequest(Guid UserId);

public record LinkLineRequest(string LinkToken, string AccessToken);
