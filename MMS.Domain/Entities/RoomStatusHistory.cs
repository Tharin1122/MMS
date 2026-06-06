using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class RoomStatusHistory : TenantEntity
{
    public Guid RoomId { get; set; }
    public RoomStatus? FromStatus { get; set; }
    public RoomStatus ToStatus { get; set; }
    public DateTime? EstimatedAvailableAt { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public Guid? BookingId { get; set; }
    public Guid? WalkInId { get; set; }

    public Room Room { get; set; } = null!;
}