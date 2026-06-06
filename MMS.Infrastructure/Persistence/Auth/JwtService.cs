using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence.Auth;

public class JwtService(IConfiguration config)
{
    public string GenerateToken(User user, IEnumerable<string> permissions)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Key"]!));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("tenantId", user.TenantId.ToString()),
            new("branchId", user.BranchId.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
        };

        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.Parse(config["Jwt:ExpiryMinutes"] ?? "60")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// สร้าง Refresh Token แบบ random (opaque token)
    /// </summary>
    public RefreshToken GenerateRefreshToken(Guid userId, string? deviceInfo = null)
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);

        return new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(randomBytes),
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(config["Jwt:RefreshTokenDays"] ?? "30")),
            DeviceInfo = deviceInfo
        };
    }
}
