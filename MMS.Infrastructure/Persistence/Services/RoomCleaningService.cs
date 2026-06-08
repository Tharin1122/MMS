using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMS.Domain.Enums;
using MMS.Domain.Entities;
using MMS.Domain.Helper;

namespace MMS.Infrastructure.Persistence.Services;

public class RoomCleaningService(
    AppDbContext db,
    IRealtimeService realtime,
    ILogger<RoomCleaningService> logger)
{
    public async Task ProcessCleaningRoomsAsync()
    {
        var now = ThaiTime.Now;

        // หาห้องที่ Cleaning อยู่
        var cleaningRooms = await db.Rooms
            .Where(r => r.CurrentStatus == RoomStatus.Cleaning
                && r.DeletedAt == null)
            .ToListAsync();

        foreach (var room in cleaningRooms)
        {
            // ดึง history ล่าสุดของห้องนี้
            var lastHistory = await db.RoomStatusHistories
                .Where(h => h.RoomId == room.Id && h.ToStatus == RoomStatus.Cleaning)
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefaultAsync();

            if (lastHistory == null) continue;

            var estimatedAvailableAt = lastHistory.EstimatedAvailableAt;
            if (estimatedAvailableAt == null) continue;

            // ถ้าหมดเวลาแล้ว → broadcast ถาม Frontend
            if (estimatedAvailableAt <= now)
            {
                logger.LogInformation(
                    "Room {Name} cleaning time up — asking frontend", room.Name);

                await realtime.NotifyCleaningCheckAsync(
                    room.BranchId, room.Id,
                    room.Name, room.CleaningBufferMins);

                // Reset EstimatedAvailableAt ใหม่ = ตอนนี้ + buffer
                // ถ้า frontend ไม่ตอบใน 60 วิ job ถัดไปจะ broadcast อีกครั้ง
                // แต่เพื่อไม่ให้ spam ทุกนาที ให้รอ CleaningBufferMins ก่อน
                lastHistory.EstimatedAvailableAt = now.AddMinutes(room.CleaningBufferMins);
                await db.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Frontend เรียกเมื่อยืนยันว่าทำความสะอาดเสร็จแล้ว
    /// </summary>
    public async Task<(bool Success, string? Error)> ConfirmCleaningDoneAsync(
        Guid roomId, Guid tenantId, Guid? confirmedBy = null)
    {
        var room = await db.Rooms
            .FirstOrDefaultAsync(r => r.Id == roomId
                && r.TenantId == tenantId
                && r.DeletedAt == null);

        if (room == null) return (false, "Room not found");
        if (room.CurrentStatus != RoomStatus.Cleaning)
            return (false, $"Room is not in Cleaning status (current: {room.CurrentStatus})");

        room.CurrentStatus = RoomStatus.Available;

        db.RoomStatusHistories.Add(new RoomStatusHistory
        {
            TenantId = room.TenantId,
            RoomId = room.Id,
            FromStatus = RoomStatus.Cleaning,
            ToStatus = RoomStatus.Available,
            ChangedBy = confirmedBy,
            ChangedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        await realtime.NotifyRoomStatusChangedAsync(
            room.BranchId, room.Id,
            room.Name,
            RoomStatus.Available.ToString());

        await realtime.SendToBranchAsync(room.BranchId, "CleaningCheckDismissed", new { roomId });

        logger.LogInformation("Room {Name} confirmed Available by user", room.Name);
        return (true, null);
    }

    /// <summary>
    /// Frontend บอกว่ายังไม่เสร็จ → reset timer
    /// </summary>
    public async Task<(bool Success, string? Error)> ExtendCleaningAsync(
        Guid roomId, Guid tenantId)
    {
        var room = await db.Rooms
            .FirstOrDefaultAsync(r => r.Id == roomId
                && r.TenantId == tenantId
                && r.DeletedAt == null);

        if (room == null) return (false, "Room not found");

        // หา history ล่าสุด แล้ว reset เวลา
        var lastHistory = await db.RoomStatusHistories
            .Where(h => h.RoomId == roomId && h.ToStatus == RoomStatus.Cleaning)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync();

        if (lastHistory != null)
        {
            lastHistory.EstimatedAvailableAt =
                ThaiTime.Now.AddMinutes(room.CleaningBufferMins);
            await db.SaveChangesAsync();
        }

        await realtime.SendToBranchAsync(room.BranchId, "CleaningCheckDismissed", new { roomId });

        logger.LogInformation(
            "Room {Name} cleaning extended {Mins} mins", room.Name, room.CleaningBufferMins);
        return (true, null);
    }
}