using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;
using MMS.Infrastructure.Persistence.Services;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TherapistController(AppDbContext db, IRealtimeService realtime) : ControllerBase
{
    // ─── CRUD ───────────────────────────────────────────────────────

    [HttpGet]
    [RequirePermission(PermissionCodes.TherapistView)]
    public async Task<IActionResult> GetAll([FromQuery] TherapistStatus? status, [FromQuery] bool? activeOnly)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var query = db.Therapists
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId && t.DeletedAt == null);

        if (status.HasValue)
            query = query.Where(t => t.CurrentStatus == status);

        if (activeOnly == true)
            query = query.Where(t => t.IsActive);

        var items = await query
            .OrderBy(t => t.DisplayName)
            .Select(t => new
            {
                t.Id, t.Code, t.DisplayName, t.Phone,
                t.AvatarUrl, t.SkillLevel, t.ExperienceYears,
                t.CurrentStatus, t.IsActive
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.TherapistView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var therapist = await db.Therapists
            .Include(t => t.TherapistServices)
                .ThenInclude(ts => ts.Service)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && t.DeletedAt == null);

        if (therapist == null) return NotFound(new { message = "Therapist not found" });
        return Ok(therapist);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.TherapistCreate)]
    public async Task<IActionResult> Create([FromBody] TherapistRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var therapist = new Therapist
        {
            TenantId = tenantId,
            BranchId = branchId,
            Code = req.Code,
            DisplayName = req.DisplayName,
            Phone = req.Phone,
            LineUserId = req.LineUserId,
            AvatarUrl = req.AvatarUrl,
            ExperienceYears = req.ExperienceYears,
            SkillLevel = req.SkillLevel
        };

        db.Therapists.Add(therapist);
        await db.SaveChangesAsync();
        return Ok(new { therapist.Id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.TherapistEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TherapistRequest req)
    {
        var tenantId = User.GetTenantId();
        var therapist = await db.Therapists
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && t.DeletedAt == null);

        if (therapist == null) return NotFound(new { message = "Therapist not found" });

        therapist.Code = req.Code;
        therapist.DisplayName = req.DisplayName;
        therapist.Phone = req.Phone;
        therapist.LineUserId = req.LineUserId;
        therapist.AvatarUrl = req.AvatarUrl;
        therapist.ExperienceYears = req.ExperienceYears;
        therapist.SkillLevel = req.SkillLevel;

        await db.SaveChangesAsync();
        return Ok(new { message = "Updated" });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.TherapistDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var therapist = await db.Therapists
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && t.DeletedAt == null);

        if (therapist == null) return NotFound(new { message = "Therapist not found" });

        therapist.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }

    // ─── Status ─────────────────────────────────────────────────────

    /// <summary>
    /// เปลี่ยนสถานะนักบำบัด เช่น Available → Break → Available
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [RequirePermission(PermissionCodes.TherapistStatusChange)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] TherapistStatusRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var therapist = await db.Therapists
            .FirstOrDefaultAsync(t => t.Id == id
                && t.TenantId == tenantId && t.DeletedAt == null);

        if (therapist == null)
            return NotFound(new { message = "Therapist not found" });

        var oldStatus = therapist.CurrentStatus;
        therapist.CurrentStatus = req.Status;

        db.TherapistStatusHistories.Add(new TherapistStatusHistory
        {
            TenantId = tenantId,
            TherapistId = id,
            FromStatus = oldStatus,
            ToStatus = req.Status,
            Reason = req.Reason,
            ChangedBy = User.GetUserId(),
            ChangedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // 🔴 Broadcast realtime
        await realtime.NotifyTherapistStatusChangedAsync(
            branchId, id,
            therapist.DisplayName,
            req.Status.ToString(),
            oldStatus.ToString());

        return Ok(new
        {
            message = "Status updated",
            status = req.Status.ToString()
        });
    }

    // ─── Schedule ───────────────────────────────────────────────────

    [HttpGet("{id:guid}/schedules")]
    [RequirePermission(PermissionCodes.TherapistScheduleView)]
    public async Task<IActionResult> GetSchedules(Guid id)
    {
        var tenantId = User.GetTenantId();
        var schedules = await db.TherapistSchedules
            .Where(s => s.TherapistId == id && s.TenantId == tenantId && s.DeletedAt == null)
            .OrderBy(s => s.DayOfWeek)
            .ToListAsync();

        return Ok(schedules);
    }

    [HttpPost("{id:guid}/schedules")]
    [RequirePermission(PermissionCodes.TherapistScheduleEdit)]
    public async Task<IActionResult> UpsertSchedule(Guid id, [FromBody] ScheduleRequest req)
    {
        var tenantId = User.GetTenantId();

        // Soft delete schedule เดิมของวันนั้น
        var existing = await db.TherapistSchedules
            .Where(s => s.TherapistId == id && s.DayOfWeek == req.DayOfWeek
                && s.TenantId == tenantId && s.DeletedAt == null)
            .ToListAsync();

        foreach (var s in existing)
            s.DeletedAt = DateTime.UtcNow;

        // สร้างใหม่
        var schedule = new TherapistSchedule
        {
            TenantId = tenantId,
            TherapistId = id,
            DayOfWeek = req.DayOfWeek,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            IsWorkday = req.IsWorkday,
            EffectiveFrom = req.EffectiveFrom,
            EffectiveTo = req.EffectiveTo
        };

        db.TherapistSchedules.Add(schedule);
        await db.SaveChangesAsync();
        return Ok(new { schedule.Id });
    }

    // ─── Leave ──────────────────────────────────────────────────────

    [HttpGet("{id:guid}/leaves")]
    [RequirePermission(PermissionCodes.TherapistLeaveManage)]
    public async Task<IActionResult> GetLeaves(Guid id, [FromQuery] LeaveStatus? status)
    {
        var tenantId = User.GetTenantId();
        var query = db.TherapistLeaves
            .Where(l => l.TherapistId == id && l.TenantId == tenantId && l.DeletedAt == null);

        if (status.HasValue)
            query = query.Where(l => l.Status == status);

        var leaves = await query.OrderByDescending(l => l.LeaveDate).ToListAsync();
        return Ok(leaves);
    }

    [HttpPost("{id:guid}/leaves")]
    [RequirePermission(PermissionCodes.TherapistLeaveManage)]
    public async Task<IActionResult> CreateLeave(Guid id, [FromBody] LeaveRequest req)
    {
        var tenantId = User.GetTenantId();

        var leave = new TherapistLeave
        {
            TenantId = tenantId,
            TherapistId = id,
            LeaveDate = req.LeaveDate,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            LeaveType = req.LeaveType,
            Reason = req.Reason
        };

        db.TherapistLeaves.Add(leave);
        await db.SaveChangesAsync();
        return Ok(new { leave.Id });
    }

    [HttpPatch("{therapistId:guid}/leaves/{leaveId:guid}/approve")]
    [RequirePermission(PermissionCodes.TherapistLeaveManage)]
    public async Task<IActionResult> ApproveLeave(Guid therapistId, Guid leaveId)
    {
        var tenantId = User.GetTenantId();
        var leave = await db.TherapistLeaves
            .FirstOrDefaultAsync(l => l.Id == leaveId && l.TherapistId == therapistId
                && l.TenantId == tenantId && l.DeletedAt == null);

        if (leave == null) return NotFound(new { message = "Leave not found" });
        if (leave.Status != LeaveStatus.Pending)
            return BadRequest(new { message = "Leave is not in pending status" });

        leave.Status = LeaveStatus.Approved;
        leave.ApprovedBy = User.GetUserId();

        await db.SaveChangesAsync();
        return Ok(new { message = "Leave approved" });
    }

    [HttpPatch("{therapistId:guid}/leaves/{leaveId:guid}/reject")]
    [RequirePermission(PermissionCodes.TherapistLeaveManage)]
    public async Task<IActionResult> RejectLeave(Guid therapistId, Guid leaveId)
    {
        var tenantId = User.GetTenantId();
        var leave = await db.TherapistLeaves
            .FirstOrDefaultAsync(l => l.Id == leaveId && l.TherapistId == therapistId
                && l.TenantId == tenantId && l.DeletedAt == null);

        if (leave == null) return NotFound(new { message = "Leave not found" });
        if (leave.Status != LeaveStatus.Pending)
            return BadRequest(new { message = "Leave is not in pending status" });

        leave.Status = LeaveStatus.Rejected;
        await db.SaveChangesAsync();
        return Ok(new { message = "Leave rejected" });
    }

    // ─── Services (ทักษะ) ───────────────────────────────────────────

    [HttpPut("{id:guid}/services")]
    [RequirePermission(PermissionCodes.TherapistEdit)]
    public async Task<IActionResult> UpdateServices(Guid id, [FromBody] List<Guid> serviceIds)
    {
        var tenantId = User.GetTenantId();

        var therapist = await db.Therapists
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && t.DeletedAt == null);
        if (therapist == null) return NotFound(new { message = "Therapist not found" });

        // ลบเดิมทั้งหมดแล้วเพิ่มใหม่
        var existing = await db.TherapistServices
            .Where(ts => ts.TherapistId == id)
            .ToListAsync();

        db.TherapistServices.RemoveRange(existing);

        foreach (var sid in serviceIds.Distinct())
        {
            db.TherapistServices.Add(new TherapistService
            {
                TenantId = tenantId,
                TherapistId = id,
                ServiceId = sid
            });
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Services updated" });
    }
}

public record TherapistRequest(
    string DisplayName,
    string? Code,
    string? Phone,
    string? LineUserId,
    string? AvatarUrl,
    int? ExperienceYears,
    SkillLevel? SkillLevel);

public record TherapistStatusRequest(TherapistStatus Status, string? Reason);

public record ScheduleRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsWorkday,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public record LeaveRequest(
    DateOnly LeaveDate,
    LeaveType LeaveType,
    string? Reason,
    TimeOnly? StartTime,
    TimeOnly? EndTime);
