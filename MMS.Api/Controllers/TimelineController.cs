using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TimelineController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// ดู Activity Timeline ของ Entity ใดๆ
    /// เช่น GET /api/timeline?entityType=Booking&entityId=xxx
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.DashboardView)]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetTenantId();

        var query = db.ActivityTimelines
            .Where(t => t.TenantId == tenantId && t.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(t => t.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(t => t.EntityId == entityId);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.EventType,
                t.EntityType,
                t.EntityId,
                t.EntityLabel,
                t.Description,
                t.ActorId,
                t.ActorName,
                t.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>
    /// ดู AuditLog ของ Entity (เห็น field ที่เปลี่ยน)
    /// เช่น GET /api/timeline/audit?entityType=User&entityId=xxx
    /// </summary>
    [HttpGet("audit")]
    [RequirePermission(PermissionCodes.SettingsView)]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetTenantId();

        var query = db.AuditLogs
            .Where(a => a.TenantId == tenantId && a.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.UserId,
                a.OldValues,
                a.NewValues,
                a.IpAddress,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }
}
