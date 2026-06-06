using MMS.Domain.Common;
using MMS.Domain.Enums;

namespace MMS.Domain.Entities;

public class BookingItem : TenantEntity
{
    public Guid BookingId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid? TherapistId { get; set; }
    public Guid? RoomId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int DurationMins { get; set; }
    public decimal Price { get; set; }
    public decimal? CommissionAmount { get; set; }
    public TherapistSelectionMode TherapistSelectionMode { get; set; } = TherapistSelectionMode.Manual;
    public int SortOrder { get; set; } = 0;

    public Booking Booking { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Therapist? Therapist { get; set; }
    public Room? Room { get; set; }
}