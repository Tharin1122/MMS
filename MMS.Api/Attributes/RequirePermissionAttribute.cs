using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MMS.Api.Attributes;

/// <summary>
/// Guard endpoint ด้วย Permission Code
/// ใช้: [RequirePermission(PermissionCodes.BookingCreate)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute(params string[] permissions) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // ยังไม่ได้ login
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "กรุณาเข้าสู่ระบบ"
            });
            return;
        }

        // ดึง permissions จาก JWT claims
        var userPermissions = user.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet();

        // ต้องมีครบทุก permission ที่ระบุ (AND logic)
        var missing = permissions.Where(p => !userPermissions.Contains(p)).ToList();

        if (missing.Any())
        {
            context.Result = new ObjectResult(new
            {
                message = "ไม่มีสิทธิ์เข้าถึง",
                required = permissions,
                missing
            })
            { StatusCode = 403 };
        }
    }
}
