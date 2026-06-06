using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MMS.Domain.Common;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core Interceptor — บันทึก AuditLog อัตโนมัติทุกครั้งที่มี SaveChanges
/// track เฉพาะ entity ที่ inherit TenantEntity (ข้อมูลของร้าน)
/// </summary>
public class AuditInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    // Entity ที่ไม่ต้อง audit (system tables)
    private static readonly HashSet<Type> _skipTypes =
    [
        typeof(AuditLog),
        typeof(ActivityTimeline),
        typeof(RefreshToken),
    ];

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not AppDbContext db)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditLogs = BuildAuditLogs(db);

        if (auditLogs.Any())
            await db.AuditLogs.AddRangeAsync(auditLogs, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditLog> BuildAuditLogs(AppDbContext db)
    {
        var http = httpContextAccessor.HttpContext;
        var userId = GetUserId(http);
        var tenantId = GetTenantId(http);
        var branchId = GetBranchId(http);
        var ip = http?.Connection.RemoteIpAddress?.ToString();
        var userAgent = http?.Request.Headers.UserAgent.ToString();

        var logs = new List<AuditLog>();

        foreach (var entry in db.ChangeTracker.Entries<TenantEntity>())
        {
            if (_skipTypes.Contains(entry.Entity.GetType())) continue;

            var action = entry.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => null
            };

            if (action == null) continue;

            // Soft delete = UPDATE แต่ควร log เป็น DELETE
            if (action == "UPDATE" && entry.Property("DeletedAt").IsModified
                && entry.Entity.DeletedAt != null)
                action = "SOFT_DELETE";

            string? oldValues = null;
            string? newValues = null;

            if (action == "UPDATE" || action == "SOFT_DELETE")
            {
                var changed = entry.Properties
                    .Where(p => p.IsModified && p.Metadata.Name != "UpdatedAt")
                    .ToDictionary(
                        p => p.Metadata.Name,
                        p => new { Old = p.OriginalValue, New = p.CurrentValue });

                if (changed.Any())
                {
                    oldValues = JsonSerializer.Serialize(
                        changed.ToDictionary(x => x.Key, x => x.Value.Old));
                    newValues = JsonSerializer.Serialize(
                        changed.ToDictionary(x => x.Key, x => x.Value.New));
                }
            }
            else if (action == "CREATE")
            {
                newValues = JsonSerializer.Serialize(
                    entry.Properties.ToDictionary(
                        p => p.Metadata.Name,
                        p => p.CurrentValue));
            }

            logs.Add(new AuditLog
            {
                TenantId = tenantId ?? entry.Entity.TenantId,
                BranchId = branchId,
                UserId = userId,
                Action = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = ip,
                UserAgent = userAgent
            });
        }

        return logs;
    }

    private static Guid? GetUserId(HttpContext? http)
    {
        var val = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(val, out var id) ? id : null;
    }

    private static Guid? GetTenantId(HttpContext? http)
    {
        var val = http?.User.FindFirstValue("tenantId");
        return Guid.TryParse(val, out var id) ? id : null;
    }

    private static Guid? GetBranchId(HttpContext? http)
    {
        var val = http?.User.FindFirstValue("branchId");
        return Guid.TryParse(val, out var id) ? id : null;
    }
}
