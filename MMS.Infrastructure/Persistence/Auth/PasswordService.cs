using Microsoft.AspNetCore.Identity;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence.Auth;

/// <summary>
/// Hash + verify password ด้วย ASP.NET Core Identity PasswordHasher (PBKDF2)
/// </summary>
public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password)
        => _hasher.HashPassword(user, password);

    public bool Verify(User user, string hash, string password)
    {
        var result = _hasher.VerifyHashedPassword(user, hash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
