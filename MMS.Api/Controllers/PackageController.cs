using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Extensions;
using MMS.Domain.Entities;
using MMS.Domain.Enums;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PackageController(AppDbContext db) : ControllerBase
{
    // GET /api/package — คอร์ส/แพ็กเกจทั้งหมดของร้าน
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = User.GetTenantId();
        var list = await db.ServicePackages
            .Where(p => p.TenantId == tenantId && p.DeletedAt == null)
            .OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Price)
            .ToListAsync();
        return Ok(list);
    }

    public record PackageRequest(string Name, string? Description, int TotalSessions,
        int? ValidityDays, decimal Price, decimal? OriginalPrice, string? ApplicableServices, bool IsFeatured);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PackageRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { message = "กรุณากรอกชื่อคอร์ส" });
        var p = new ServicePackage
        {
            TenantId = User.GetTenantId(),
            Name = r.Name.Trim(), Description = r.Description, TotalSessions = Math.Max(1, r.TotalSessions),
            ValidityDays = r.ValidityDays, Price = r.Price, OriginalPrice = r.OriginalPrice,
            ApplicableServices = r.ApplicableServices, IsFeatured = r.IsFeatured,
        };
        db.ServicePackages.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PackageRequest r)
    {
        var tenantId = User.GetTenantId();
        var p = await db.ServicePackages.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.DeletedAt == null);
        if (p == null) return NotFound(new { message = "ไม่พบคอร์ส" });
        p.Name = r.Name.Trim(); p.Description = r.Description; p.TotalSessions = Math.Max(1, r.TotalSessions);
        p.ValidityDays = r.ValidityDays; p.Price = r.Price; p.OriginalPrice = r.OriginalPrice;
        p.ApplicableServices = r.ApplicableServices; p.IsFeatured = r.IsFeatured;
        await db.SaveChangesAsync();
        return Ok(new { message = "updated" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var p = await db.ServicePackages.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.DeletedAt == null);
        if (p == null) return NotFound(new { message = "ไม่พบคอร์ส" });
        p.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "deleted" });
    }

    public record SellRequest(Guid CustomerId, string? PaymentMethod);

    // POST /api/package/{id}/sell — ขายคอร์สให้ลูกค้า → สร้าง CustomerPackage + ใบเสร็จจริง
    [HttpPost("{id:guid}/sell")]
    public async Task<IActionResult> Sell(Guid id, [FromBody] SellRequest r)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();
        var pkg = await db.ServicePackages.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.DeletedAt == null);
        if (pkg == null) return NotFound(new { message = "ไม่พบคอร์ส" });
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == r.CustomerId && c.TenantId == tenantId && c.DeletedAt == null);
        if (customer == null) return BadRequest(new { message = "ไม่พบลูกค้า" });

        Enum.TryParse<PaymentMethod>(r.PaymentMethod ?? "Cash", out var method);
        var now = DateTime.UtcNow;

        // ใบเสร็จจริง → เข้ารายรับ
        var payment = new Payment
        {
            TenantId = tenantId, BranchId = branchId, CustomerId = customer.Id,
            ReceiptNo = "PKG" + now.ToString("yyMMddHHmmss"),
            SubTotal = pkg.Price, TotalAmount = pkg.Price, PaidAmount = pkg.Price,
            PaymentMethod = method, Status = PaymentStatus.Paid, PaidAt = now,
            CashierId = User.GetUserId(),
            Items = new List<PaymentItem>
            {
                new() { TenantId = tenantId, RefType = "Package", ServiceName = pkg.Name,
                    UnitPrice = pkg.Price, Quantity = 1, LineTotal = pkg.Price }
            }
        };
        db.Payments.Add(payment);

        var cp = new CustomerPackage
        {
            TenantId = tenantId, BranchId = branchId, CustomerId = customer.Id, ServicePackageId = pkg.Id,
            PackageName = pkg.Name, TotalSessions = pkg.TotalSessions, RemainingSessions = pkg.TotalSessions,
            PricePaid = pkg.Price, PurchasedAt = now,
            ExpiresAt = pkg.ValidityDays.HasValue ? now.AddDays(pkg.ValidityDays.Value) : null,
            PaymentId = payment.Id,
        };
        db.CustomerPackages.Add(cp);

        pkg.SoldCount += 1;
        customer.TotalSpent += pkg.Price;
        await db.SaveChangesAsync();

        return Ok(new { message = "ขายคอร์สแล้ว", receiptNo = payment.ReceiptNo, customerPackageId = cp.Id });
    }

    // GET /api/package/customer/{customerId} — คอร์สคงเหลือของลูกค้า
    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> CustomerPackages(Guid customerId)
    {
        var tenantId = User.GetTenantId();
        var now = DateTime.UtcNow;
        var list = await db.CustomerPackages
            .Where(c => c.CustomerId == customerId && c.TenantId == tenantId && c.DeletedAt == null
                && c.RemainingSessions > 0 && (c.ExpiresAt == null || c.ExpiresAt > now))
            .OrderBy(c => c.ExpiresAt)
            .ToListAsync();
        return Ok(list);
    }

    // POST /api/package/redeem/{customerPackageId} — ตัดคอร์ส 1 ครั้ง (ใช้ตอน POS/เช็คเอาท์)
    [HttpPost("redeem/{customerPackageId:guid}")]
    public async Task<IActionResult> Redeem(Guid customerPackageId)
    {
        var tenantId = User.GetTenantId();
        var cp = await db.CustomerPackages.FirstOrDefaultAsync(c => c.Id == customerPackageId && c.TenantId == tenantId && c.DeletedAt == null);
        if (cp == null) return NotFound(new { message = "ไม่พบคอร์สของลูกค้า" });
        if (cp.RemainingSessions <= 0) return BadRequest(new { message = "คอร์สนี้ใช้ครบแล้ว" });
        if (cp.ExpiresAt.HasValue && cp.ExpiresAt < DateTime.UtcNow) return BadRequest(new { message = "คอร์สหมดอายุแล้ว" });
        cp.RemainingSessions -= 1;
        await db.SaveChangesAsync();
        return Ok(new { cp.RemainingSessions, message = "ตัดคอร์สแล้ว เหลือ " + cp.RemainingSessions + " ครั้ง" });
    }
}
