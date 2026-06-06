using System.Security.Claims;

namespace MMS.Api.Extensions;

/// <summary>
/// Extension methods ดึงข้อมูล User จาก JWT Claims ได้สะดวก
/// </summary>
public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Guid GetTenantId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue("tenantId")!);

    public static Guid GetBranchId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue("branchId")!);

    public static string GetDisplayName(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public static IEnumerable<string> GetPermissions(this ClaimsPrincipal user)
        => user.Claims.Where(c => c.Type == "permission").Select(c => c.Value);

    public static bool HasPermission(this ClaimsPrincipal user, string permissionCode)
        => user.Claims.Any(c => c.Type == "permission" && c.Value == permissionCode);
}
