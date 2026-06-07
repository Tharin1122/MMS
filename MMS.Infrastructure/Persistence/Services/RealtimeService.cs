namespace MMS.Infrastructure.Persistence.Services;

public interface IRealtimeService
{
    Task NotifyTherapistStatusChangedAsync(Guid branchId, Guid therapistId, string displayName, string newStatus, string? oldStatus = null);
    Task NotifyRoomStatusChangedAsync(Guid branchId, Guid roomId, string roomName, string newStatus, DateTime? estimatedAvailableAt = null);
    Task NotifyQueueUpdatedAsync(Guid branchId, Guid walkInId, string queueNo, string customerName, string status, int? estimatedWaitMins = null);
    Task NotifyBookingUpdatedAsync(Guid branchId, Guid bookingId, string bookingNo, string status, string customerName);
    Task NotifyDashboardSnapshotAsync(Guid branchId, object snapshot);
    Task SendToBranchAsync(Guid branchId, string eventName, object data);
    Task SendToTenantAsync(Guid tenantId, string eventName, object data);
}
