using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Infrastructure.Persistence.Services;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvailabilityController(AvailabilityService availabilityService) : ControllerBase
{
    /// <summary>
    /// ดึง Therapist ที่ว่างในช่วงเวลาที่กำหนด
    /// GET /api/availability/therapists?date=2026-06-07&startTime=10:00&durationMins=60&serviceId=xxx
    /// </summary>
    [HttpGet("therapists")]
    [RequirePermission(PermissionCodes.BookingCreate)]
    public async Task<IActionResult> GetAvailableTherapists(
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly startTime,
        [FromQuery] int durationMins,
        [FromQuery] Guid? serviceId)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        if (durationMins <= 0)
            return BadRequest(new { message = "durationMins must be greater than 0" });

        var result = await availabilityService.GetAvailableTherapistsAsync(
            tenantId, branchId, date, startTime, durationMins, serviceId);

        return Ok(new
        {
            date,
            startTime,
            endTime = startTime.AddMinutes(durationMins),
            durationMins,
            available = result.Count,
            therapists = result
        });
    }

    /// <summary>
    /// ดึง Room ที่ว่างในช่วงเวลาที่กำหนด
    /// GET /api/availability/rooms?date=2026-06-07&startTime=10:00&durationMins=60&roomType=Normal
    /// </summary>
    [HttpGet("rooms")]
    [RequirePermission(PermissionCodes.BookingCreate)]
    public async Task<IActionResult> GetAvailableRooms(
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly startTime,
        [FromQuery] int durationMins,
        [FromQuery] string? roomType)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        if (durationMins <= 0)
            return BadRequest(new { message = "durationMins must be greater than 0" });

        var result = await availabilityService.GetAvailableRoomsAsync(
            tenantId, branchId, date, startTime, durationMins, roomType);

        return Ok(new
        {
            date,
            startTime,
            endTime = startTime.AddMinutes(durationMins),
            durationMins,
            available = result.Count,
            rooms = result
        });
    }

    /// <summary>
    /// ดูตารางงานทั้งหมดของวันที่กำหนด (ใช้แสดง Dashboard)
    /// GET /api/availability/day-schedule?date=2026-06-07
    /// </summary>
    [HttpGet("day-schedule")]
    [RequirePermission(PermissionCodes.DashboardView)]
    public async Task<IActionResult> GetDaySchedule([FromQuery] DateOnly date)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var result = await availabilityService.GetDayScheduleAsync(tenantId, branchId, date);

        return Ok(new
        {
            date,
            totalTherapists = result.Count,
            workingToday = result.Count(t => t.IsWorkday),
            onLeave = result.Count(t => t.IsOnLeave),
            schedule = result
        });
    }
}
