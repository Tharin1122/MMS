using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoomController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.RoomView)]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var query = db.Rooms
            .Where(r => r.TenantId == tenantId && r.BranchId == branchId && r.DeletedAt == null);

        if (activeOnly == true)
            query = query.Where(r => r.IsActive);

        var items = await query
            .OrderBy(r => r.RoomType)
            .ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id, r.Name, r.RoomType, r.Capacity,
                r.CleaningBufferMins, r.CurrentStatus, r.IsActive
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.RoomView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var room = await db.Rooms
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && r.DeletedAt == null);

        if (room == null) return NotFound(new { message = "Room not found" });
        return Ok(room);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.RoomCreate)]
    public async Task<IActionResult> Create([FromBody] RoomRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var room = new Room
        {
            TenantId = tenantId,
            BranchId = branchId,
            Name = req.Name,
            RoomType = req.RoomType,
            Capacity = req.Capacity,
            CleaningBufferMins = req.CleaningBufferMins
        };

        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return Ok(new { room.Id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.RoomEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoomRequest req)
    {
        var tenantId = User.GetTenantId();
        var room = await db.Rooms
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && r.DeletedAt == null);

        if (room == null) return NotFound(new { message = "Room not found" });

        room.Name = req.Name;
        room.RoomType = req.RoomType;
        room.Capacity = req.Capacity;
        room.CleaningBufferMins = req.CleaningBufferMins;
        room.IsActive = req.IsActive;

        await db.SaveChangesAsync();
        return Ok(new { message = "Updated" });
    }

    /// <summary>
    /// เปลี่ยนสถานะห้อง เช่น Available → Cleaning → Available
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [RequirePermission(PermissionCodes.RoomStatusChange)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] RoomStatusRequest req)
    {
        var tenantId = User.GetTenantId();
        var room = await db.Rooms
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && r.DeletedAt == null);

        if (room == null) return NotFound(new { message = "Room not found" });

        var oldStatus = room.CurrentStatus;
        room.CurrentStatus = req.Status;

        // บันทึก history
        db.RoomStatusHistories.Add(new RoomStatusHistory
        {
            TenantId = tenantId,
            RoomId = id,
            FromStatus = oldStatus,
            ToStatus = req.Status,
            ChangedBy = User.GetUserId()
        });

        await db.SaveChangesAsync();
        return Ok(new { message = "Status updated", status = req.Status.ToString() });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.RoomEdit)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var room = await db.Rooms
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && r.DeletedAt == null);

        if (room == null) return NotFound(new { message = "Room not found" });

        room.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }
}

public record RoomRequest(
    string Name,
    RoomType RoomType,
    int Capacity,
    int CleaningBufferMins,
    bool IsActive = true);

public record RoomStatusRequest(RoomStatus Status, string? Reason);
