using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;
using MMS.Infrastructure.Persistence.Services;

namespace MMS.Api.Controllers;

// ══════════════════════════════════════════════
// WALK-IN CONTROLLER
// ══════════════════════════════════════════════
[ApiController]
[Route("api/walk-in")]
[Authorize]
public class WalkInController(AppDbContext db, WalkInService walkInService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.WalkInView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] WalkInStatus? status,
        [FromQuery] DateOnly? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var query = db.WalkIns
            .Where(w => w.TenantId == tenantId
                && w.BranchId == branchId
                && w.DeletedAt == null);

        if (status.HasValue)
            query = query.Where(w => w.Status == status);

        if (date.HasValue)
        {
            var from = date.Value.ToDateTime(TimeOnly.MinValue);
            var to = date.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(w => w.ArrivalTime >= from && w.ArrivalTime <= to);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(w => w.ArrivalTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new
            {
                w.Id, w.QueueNo, w.Status,
                w.ArrivalTime, w.StartTime, w.EndTime,
                w.EstimatedWaitMins, w.TotalAmount,
                Customer = new { w.Customer.Id, w.Customer.DisplayName, w.Customer.Phone },
                ItemCount = w.Items.Count
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.WalkInView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var walkIn = await db.WalkIns
            .Include(w => w.Customer)
            .Include(w => w.Items)
                .ThenInclude(i => i.Service)
            .Include(w => w.Items)
                .ThenInclude(i => i.Therapist)
            .Include(w => w.Items)
                .ThenInclude(i => i.Room)
            .FirstOrDefaultAsync(w => w.Id == id
                && w.TenantId == tenantId && w.DeletedAt == null);

        if (walkIn == null)
            return NotFound(new { message = "WalkIn not found" });

        return Ok(new
        {
            walkIn.Id, walkIn.QueueNo, walkIn.Status,
            walkIn.ArrivalTime, walkIn.StartTime, walkIn.EndTime,
            walkIn.EstimatedWaitMins, walkIn.TotalAmount, walkIn.Notes,
            Customer = new
            {
                walkIn.Customer.Id,
                walkIn.Customer.DisplayName,
                walkIn.Customer.Phone
            },
            Items = walkIn.Items.OrderBy(i => i.SortOrder).Select(i => new
            {
                i.Id, i.SortOrder, i.StartTime, i.EndTime,
                i.DurationMins, i.Price, i.CommissionAmount,
                Service = new { i.Service.Id, i.Service.Name },
                Therapist = i.Therapist == null ? null
                    : new { i.Therapist.Id, i.Therapist.DisplayName, i.Therapist.Code },
                Room = i.Room == null ? null
                    : new { i.Room.Id, i.Room.Name }
            })
        });
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Create([FromBody] CreateWalkInRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "WalkIn must have at least 1 item" });

        var result = await walkInService.CreateWalkInAsync(tenantId, branchId, req);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new
        {
            message = "Walk-in created",
            walkInId = result.WalkInId,
            queueNo = result.QueueNo,
            estimatedWaitMins = result.EstimatedWaitMins
        });
    }

    /// <summary>
    /// Assign therapist ให้ walk-in (เมื่อมีช่างว่าง)
    /// </summary>
    [HttpPatch("{id:guid}/assign")]
    [RequirePermission(PermissionCodes.WalkInAssign)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest req)
    {
        var (ok, err) = await walkInService.AssignTherapistAsync(
            id, User.GetTenantId(), req.TherapistId, req.RoomId);
        return ok ? Ok(new { message = "Therapist assigned" })
                  : BadRequest(new { message = err });
    }

    /// <summary>
    /// เริ่มให้บริการ
    /// </summary>
    [HttpPatch("{id:guid}/start")]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Start(Guid id)
    {
        var (ok, err) = await walkInService.StartServiceAsync(id, User.GetTenantId());
        return ok ? Ok(new { message = "Service started" })
                  : BadRequest(new { message = err });
    }

    /// <summary>
    /// เสร็จสิ้นการบริการ
    /// </summary>
    [HttpPatch("{id:guid}/complete")]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Complete(Guid id)
    {
        var (ok, err) = await walkInService.CompleteAsync(id, User.GetTenantId());
        return ok ? Ok(new { message = "Service completed" })
                  : BadRequest(new { message = err });
    }

    /// <summary>
    /// ยกเลิก
    /// </summary>
    [HttpPatch("{id:guid}/cancel")]
    [RequirePermission(PermissionCodes.WalkInCreate)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelWalkInRequest req)
    {
        var (ok, err) = await walkInService.CancelAsync(
            id, User.GetTenantId(), req.Reason);
        return ok ? Ok(new { message = "Walk-in cancelled" })
                  : BadRequest(new { message = err });
    }
}

// ══════════════════════════════════════════════
// QUEUE CONTROLLER — ภาพรวม Queue ของวันนี้
// ══════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QueueController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// ดู Queue ทั้งหมดของวันนี้แบบ real-time
    /// GET /api/queue
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.QueueView)]
    public async Task<IActionResult> GetTodayQueue()
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from = today.ToDateTime(TimeOnly.MinValue);
        var to = today.ToDateTime(TimeOnly.MaxValue);

        // Waiting queue
        var waiting = await db.WalkIns
            .Where(w => w.TenantId == tenantId && w.BranchId == branchId
                && w.Status == WalkInStatus.Waiting
                && w.ArrivalTime >= from && w.ArrivalTime <= to
                && w.DeletedAt == null)
            .OrderBy(w => w.ArrivalTime)
            .Select(w => new
            {
                w.Id, w.QueueNo, w.ArrivalTime, w.EstimatedWaitMins,
                Customer = new { w.Customer.DisplayName, w.Customer.Phone },
                Services = w.Items.Select(i => i.Service.Name)
            })
            .ToListAsync();

        // In service
        var inService = await db.WalkIns
            .Where(w => w.TenantId == tenantId && w.BranchId == branchId
                && w.Status == WalkInStatus.InService
                && w.DeletedAt == null)
            .Select(w => new
            {
                w.Id, w.QueueNo, w.StartTime, w.EndTime,
                Customer = new { w.Customer.DisplayName },
                Therapists = w.Items
                    .Where(i => i.Therapist != null)
                    .Select(i => new { i.Therapist!.DisplayName, i.Therapist.Code })
            })
            .ToListAsync();

        // Therapist status summary
        var therapists = await db.Therapists
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId
                && t.IsActive && t.DeletedAt == null)
            .Select(t => new
            {
                t.Id, t.DisplayName, t.Code,
                t.AvatarUrl, t.CurrentStatus
            })
            .ToListAsync();

        return Ok(new
        {
            date = today,
            summary = new
            {
                waitingCount = waiting.Count,
                inServiceCount = inService.Count,
                availableTherapists = therapists.Count(t =>
                    t.CurrentStatus == TherapistStatus.Available),
                totalTherapists = therapists.Count
            },
            waiting,
            inService,
            therapists
        });
    }

    /// <summary>
    /// จัดเรียง Queue ใหม่
    /// </summary>
    [HttpPatch("reorder")]
    [RequirePermission(PermissionCodes.QueueManage)]
    public async Task<IActionResult> Reorder([FromBody] List<QueueOrderItem> order)
    {
        var tenantId = User.GetTenantId();

        foreach (var item in order)
        {
            var walkIn = await db.WalkIns
                .FirstOrDefaultAsync(w => w.Id == item.WalkInId
                    && w.TenantId == tenantId
                    && w.Status == WalkInStatus.Waiting
                    && w.DeletedAt == null);

            if (walkIn != null)
                walkIn.EstimatedWaitMins = item.EstimatedWaitMins;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Queue reordered" });
    }
}

// Records
public record AssignRequest(Guid TherapistId, Guid? RoomId);
public record CancelWalkInRequest(string? Reason);
public record QueueOrderItem(Guid WalkInId, int EstimatedWaitMins);
