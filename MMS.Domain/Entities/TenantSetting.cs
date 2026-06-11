using MMS.Domain.Common;

namespace MMS.Domain.Entities;

/// <summary>
/// การตั้งค่าร้านต่อ tenant — เก็บเป็น JSON blob (ข้อมูลร้าน, เวลาทำการ, กฎจอง, toggle ต่างๆ)
/// </summary>
public class TenantSetting : TenantEntity
{
    public string SettingsJson { get; set; } = "{}";
}
