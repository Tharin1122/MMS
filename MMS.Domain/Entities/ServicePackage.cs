using MMS.Domain.Common;

namespace MMS.Domain.Entities;

/// <summary>
/// คอร์ส / แพ็กเกจซื้อล่วงหน้า (เช่น นวดน้ำมัน 10 ครั้ง) — ลูกค้าซื้อแล้วตัดครั้งทีหลัง
/// </summary>
public class ServicePackage : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int TotalSessions { get; set; }          // จำนวนครั้งทั้งหมดในคอร์ส
    public int? ValidityDays { get; set; }           // อายุคอร์ส (วัน) — null = ไม่มีหมดอายุ
    public decimal Price { get; set; }               // ราคาขาย
    public decimal? OriginalPrice { get; set; }      // ราคาปกติ (ก่อนลด) สำหรับแสดงขีดฆ่า
    public string? ApplicableServices { get; set; }  // ชื่อบริการที่ใช้ได้ (คั่นด้วย ,) — null = ทุกบริการ
    public bool IsFeatured { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SoldCount { get; set; } = 0;
}
