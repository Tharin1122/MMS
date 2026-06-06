using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class Room : TenantEntity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public RoomType RoomType { get; set; }
    public int Capacity { get; set; } = 1;
    public int CleaningBufferMins { get; set; } = 10;
    public RoomStatus CurrentStatus { get; set; } = RoomStatus.Available;
    public bool IsActive { get; set; } = true;

    public Branch Branch { get; set; } = null!;
    public ICollection<RoomStatusHistory> StatusHistories { get; set; } = [];
}