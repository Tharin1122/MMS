using Microsoft.AspNetCore.SignalR;
using MMS.Api.Hubs;
using MMS.Infrastructure.Persistence.Services;

namespace MMS.Api.Services;

public class RealtimeService(IHubContext<MmsHub> hub) : IRealtimeService
{
    public async Task NotifyTherapistStatusChangedAsync(
        Guid branchId, Guid therapistId, string displayName,
        string newStatus, string? oldStatus = null)
        => await hub.Clients.Group($"branch_{branchId}")
            .SendAsync("TherapistStatusChanged", new
            { therapistId, displayName, newStatus, oldStatus, changedAt = DateTime.UtcNow });

    public async Task NotifyRoomStatusChangedAsync(
        Guid branchId, Guid roomId, string roomName,
        string newStatus, DateTime? estimatedAvailableAt = null)
        => await hub.Clients.Group($"branch_{branchId}")
            .SendAsync("RoomStatusChanged", new
            { roomId, roomName, newStatus, estimatedAvailableAt, changedAt = DateTime.UtcNow });

    public async Task NotifyQueueUpdatedAsync(
        Guid branchId, Guid walkInId, string queueNo,
        string customerName, string status, int? estimatedWaitMins = null)
        => await hub.Clients.Group($"branch_{branchId}")
            .SendAsync("QueueUpdated", new
            { walkInId, queueNo, customerName, status, estimatedWaitMins, updatedAt = DateTime.UtcNow });

    public async Task NotifyBookingUpdatedAsync(
        Guid branchId, Guid bookingId, string bookingNo,
        string status, string customerName)
        => await hub.Clients.Group($"branch_{branchId}")
            .SendAsync("BookingUpdated", new
            { bookingId, bookingNo, status, customerName, updatedAt = DateTime.UtcNow });

    public async Task SendToBranchAsync(Guid branchId, string eventName, object data)
        => await hub.Clients.Group($"branch_{branchId}").SendAsync(eventName, data);

    public async Task SendToTenantAsync(Guid tenantId, string eventName, object data)
        => await hub.Clients.Group($"tenant_{tenantId}").SendAsync(eventName, data);
}