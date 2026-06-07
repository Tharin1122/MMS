using Microsoft.EntityFrameworkCore;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;
using MMS.Domain.Helper;

namespace MMS.Infrastructure.Persistence.Services;

/// <summary>
/// Availability Engine — ตรวจสอบว่า Therapist / Room ว่างหรือไม่
/// ใช้ใน Phase 2 (Schedule Query) และ Phase 3 (Booking Engine)
/// </summary>
public class AvailabilityService(AppDbContext db)
{
    // ──────────────────────────────────────────────
    // THERAPIST
    // ──────────────────────────────────────────────

    /// <summary>
    /// ดึง Therapist ที่ว่างในช่วงเวลาที่กำหนด
    /// </summary>
    public async Task<List<TherapistSlot>> GetAvailableTherapistsAsync(
        Guid tenantId,
        Guid branchId,
        DateOnly date,
        TimeOnly startTime,
        int durationMins,
        Guid? serviceId = null)
    {
        var endTime = startTime.AddMinutes(durationMins);
        var dayOfWeek = date.DayOfWeek;

        // 1. ดึง therapist ทั้งหมดที่ active ในสาขา
        var therapistsQuery = db.Therapists
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId
                && t.IsActive && t.DeletedAt == null);

        // กรองตาม service ที่ therapist ทำได้
        if (serviceId.HasValue)
            therapistsQuery = therapistsQuery
                .Where(t => t.TherapistServices
                    .Any(ts => ts.ServiceId == serviceId && ts.IsActive));

        var therapists = await therapistsQuery
            .Include(t => t.Schedules)
            .Include(t => t.Leaves)
            .ToListAsync();

        var result = new List<TherapistSlot>();

        foreach (var t in therapists)
        {
            var slot = await CheckTherapistAvailabilityAsync(
                t, tenantId, date, dayOfWeek, startTime, endTime, durationMins);

            if (slot != null)
                result.Add(slot);
        }

        return result.OrderBy(s => s.DisplayName).ToList();
    }

    /// <summary>
    /// ตรวจ therapist คนเดียวว่าว่างหรือไม่
    /// </summary>
    public async Task<TherapistSlot?> CheckTherapistAvailabilityAsync(
        Therapist therapist,
        Guid tenantId,
        DateOnly date,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int durationMins)
    {
        // 1. ตรวจตารางงาน (schedule)
        var schedule = therapist.Schedules
            .Where(s => s.DayOfWeek == dayOfWeek
                && s.IsWorkday
                && s.EffectiveFrom <= date
                && (s.EffectiveTo == null || s.EffectiveTo >= date)
                && s.DeletedAt == null)
            .FirstOrDefault();

        if (schedule == null) return null; // ไม่มีตารางงานวันนี้

        // เช็คว่า startTime-endTime อยู่ใน schedule หรือไม่
        if (startTime < schedule.StartTime || endTime > schedule.EndTime)
            return null;

        // 2. ตรวจการลา
        var onLeave = therapist.Leaves.Any(l =>
            l.LeaveDate == date
            && l.Status == LeaveStatus.Approved
            && l.DeletedAt == null
            && (l.LeaveType == LeaveType.FullDay ||
                (l.StartTime <= startTime && l.EndTime >= endTime)));

        if (onLeave) return null;

        // 3. ตรวจ Booking ที่ชนกัน
        var hasBookingConflict = await db.BookingItems
            .AnyAsync(bi =>
                bi.TherapistId == therapist.Id
                && bi.Booking.BookingDate == date
                && bi.Booking.Status != BookingStatus.Cancelled
                && bi.Booking.DeletedAt == null
                && bi.DeletedAt == null
                && bi.StartTime < endTime
                && bi.EndTime > startTime);

        if (hasBookingConflict) return null;

        // 4. ตรวจ WalkIn ที่กำลังให้บริการ
        var hasWalkInConflict = await db.WalkInItems
            .AnyAsync(wi =>
                wi.TherapistId == therapist.Id
                && wi.WalkIn.Status == WalkInStatus.InService
                && wi.WalkIn.DeletedAt == null
                && wi.DeletedAt == null
                && wi.StartTime < ThaiTime.Today.ToDateTime(endTime)
                && wi.EndTime > ThaiTime.Today.ToDateTime(startTime));

        if (hasWalkInConflict) return null;

        return new TherapistSlot
        {
            TherapistId = therapist.Id,
            DisplayName = therapist.DisplayName,
            Code = therapist.Code,
            AvatarUrl = therapist.AvatarUrl,
            SkillLevel = therapist.SkillLevel,
            CurrentStatus = therapist.CurrentStatus,
            WorkStart = schedule.StartTime,
            WorkEnd = schedule.EndTime
        };
    }

    // ──────────────────────────────────────────────
    // ROOM
    // ──────────────────────────────────────────────

    /// <summary>
    /// ดึง Room ที่ว่างในช่วงเวลาที่กำหนด
    /// </summary>
    public async Task<List<RoomSlot>> GetAvailableRoomsAsync(
        Guid tenantId,
        Guid branchId,
        DateOnly date,
        TimeOnly startTime,
        int durationMins,
        string? requiredRoomType = null)
    {
        var endTime = startTime.AddMinutes(durationMins);

        var roomsQuery = db.Rooms
            .Where(r => r.TenantId == tenantId && r.BranchId == branchId
                && r.IsActive && r.DeletedAt == null
                && r.CurrentStatus == RoomStatus.Available);

        if (!string.IsNullOrWhiteSpace(requiredRoomType)
            && Enum.TryParse<RoomType>(requiredRoomType, out var roomType))
            roomsQuery = roomsQuery.Where(r => r.RoomType == roomType);

        var rooms = await roomsQuery.ToListAsync();
        var result = new List<RoomSlot>();

        foreach (var room in rooms)
        {
            // ตรวจ Booking ที่ชนกัน (รวม cleaning buffer)
            var hasBookingConflict = await db.BookingItems
                .AnyAsync(bi =>
                    bi.RoomId == room.Id
                    && bi.Booking.BookingDate == date
                    && bi.Booking.Status != BookingStatus.Cancelled
                    && bi.Booking.DeletedAt == null
                    && bi.DeletedAt == null
                    && bi.StartTime < endTime.AddMinutes(room.CleaningBufferMins)
                    && bi.EndTime > startTime);

            if (hasBookingConflict) continue;

            result.Add(new RoomSlot
            {
                RoomId = room.Id,
                Name = room.Name,
                RoomType = room.RoomType,
                Capacity = room.Capacity,
                CurrentStatus = room.CurrentStatus
            });
        }

        return result.OrderBy(r => r.RoomType).ThenBy(r => r.Name).ToList();
    }

    // ──────────────────────────────────────────────
    // DAILY SCHEDULE
    // ──────────────────────────────────────────────

    /// <summary>
    /// ดูตารางงานของ therapist ทั้งหมดในวันที่กำหนด
    /// แสดง slots ที่จองแล้วและที่ว่าง
    /// </summary>
    public async Task<List<TherapistDaySchedule>> GetDayScheduleAsync(
        Guid tenantId,
        Guid branchId,
        DateOnly date)
    {
        var dayOfWeek = date.DayOfWeek;

        var therapists = await db.Therapists
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId
                && t.IsActive && t.DeletedAt == null)
            .Include(t => t.Schedules)
            .Include(t => t.Leaves)
            .ToListAsync();

        // ดึง booking items ของวันนี้
        var bookingItems = await db.BookingItems
            .Where(bi =>
                bi.Booking.TenantId == tenantId
                && bi.Booking.BranchId == branchId
                && bi.Booking.BookingDate == date
                && bi.Booking.Status != BookingStatus.Cancelled
                && bi.Booking.DeletedAt == null
                && bi.DeletedAt == null)
            .Include(bi => bi.Booking)
            .Include(bi => bi.Service)
            .ToListAsync();

        // ดึง walkin items ของวันนี้
        var walkInItems = await db.WalkInItems
            .Where(wi =>
                wi.WalkIn.TenantId == tenantId
                && wi.WalkIn.BranchId == branchId
                && wi.WalkIn.Status == WalkInStatus.InService
                && wi.WalkIn.DeletedAt == null
                && wi.DeletedAt == null)
            .Include(wi => wi.WalkIn)
            .Include(wi => wi.Service)
            .ToListAsync();

        var result = new List<TherapistDaySchedule>();

        foreach (var t in therapists)
        {
            var schedule = t.Schedules.FirstOrDefault(s =>
            s.DayOfWeek == dayOfWeek
            && s.IsWorkday
            && s.EffectiveFrom <= date
            && (s.EffectiveTo == null || s.EffectiveTo >= date)
            && s.DeletedAt == null);
            var onLeave = t.Leaves.Any(l =>
            l.LeaveDate == date
            && l.Status == LeaveStatus.Approved
            && l.DeletedAt == null);

            var myBookings = bookingItems
                .Where(bi => bi.TherapistId == t.Id)
                .Select(bi => new ScheduledSlot
                {
                    Type = "booking",
                    StartTime = bi.StartTime,
                    EndTime = bi.EndTime,
                    ServiceName = bi.Service.Name,
                    BookingId = bi.BookingId,
                    Status = bi.Booking.Status.ToString()
                })
                .ToList();

            var myWalkIns = walkInItems
                .Where(wi => wi.TherapistId == t.Id && wi.StartTime.HasValue)
                .Select(wi => new ScheduledSlot
                {
                    Type = "walkin",
                    StartTime = TimeOnly.FromDateTime(wi.StartTime!.Value),
                    EndTime = TimeOnly.FromDateTime(wi.EndTime!.Value),
                    ServiceName = wi.Service.Name,
                    WalkInId = wi.WalkInId,
                    Status = wi.WalkIn.Status.ToString()
                })
                .ToList();

            result.Add(new TherapistDaySchedule
            {
                TherapistId = t.Id,
                DisplayName = t.DisplayName,
                Code = t.Code,
                AvatarUrl = t.AvatarUrl,
                CurrentStatus = t.CurrentStatus,
                IsWorkday = schedule != null && !onLeave,
                IsOnLeave = onLeave,
                WorkStart = schedule?.StartTime,
                WorkEnd = schedule?.EndTime,
                Slots = myBookings.Concat(myWalkIns)
                    .OrderBy(s => s.StartTime)
                    .ToList()
            });
        }

        return result.OrderBy(t => t.DisplayName).ToList();
    }
}

// ──────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────

public record TherapistSlot
{
    public Guid TherapistId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? AvatarUrl { get; init; }
    public SkillLevel? SkillLevel { get; init; }
    public TherapistStatus CurrentStatus { get; init; }
    public TimeOnly WorkStart { get; init; }
    public TimeOnly WorkEnd { get; init; }
}

public record RoomSlot
{
    public Guid RoomId { get; init; }
    public string Name { get; init; } = string.Empty;
    public RoomType RoomType { get; init; }
    public int Capacity { get; init; }
    public RoomStatus CurrentStatus { get; init; }
}

public record TherapistDaySchedule
{
    public Guid TherapistId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? AvatarUrl { get; init; }
    public TherapistStatus CurrentStatus { get; init; }
    public bool IsWorkday { get; init; }
    public bool IsOnLeave { get; init; }
    public TimeOnly? WorkStart { get; init; }
    public TimeOnly? WorkEnd { get; init; }
    public List<ScheduledSlot> Slots { get; init; } = [];
}

public record ScheduledSlot
{
    public string Type { get; init; } = string.Empty; // "booking" | "walkin"
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public Guid? BookingId { get; init; }
    public Guid? WalkInId { get; init; }
    public string Status { get; init; } = string.Empty;
}
