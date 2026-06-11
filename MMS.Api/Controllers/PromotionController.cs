using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Extensions;
using MMS.Domain.Entities;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PromotionController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tid = User.GetTenantId();
        var promos = await db.Promotions.Where(p => p.TenantId == tid && p.DeletedAt == null).OrderByDescending(p => p.CreatedAt).ToListAsync();
        var coupons = await db.Coupons.Where(c => c.TenantId == tid && c.DeletedAt == null).OrderByDescending(c => c.CreatedAt).ToListAsync();
        var summary = new
        {
            activePromos = promos.Count(p => p.IsActive),
            totalSold = promos.Sum(p => p.SoldCount),
            couponsUsed = coupons.Sum(c => c.UsedCount),
        };
        return Ok(new { summary, promos, coupons });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePromo([FromBody] PromotionRequest r)
    {
        var p = new Promotion { TenantId = User.GetTenantId(), Title = r.Title, Description = r.Description, Code = r.Code, DiscountPercent = r.DiscountPercent, DiscountAmount = r.DiscountAmount, ExpiresAt = r.ExpiresAt, IsActive = true };
        db.Promotions.Add(p); await db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePromo(Guid id, [FromBody] PromotionRequest r)
    {
        var tid = User.GetTenantId();
        var p = await db.Promotions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tid && x.DeletedAt == null);
        if (p == null) return NotFound(new { message = "ไม่พบโปรโมชัน" });
        p.Title = r.Title; p.Description = r.Description; p.Code = r.Code; p.DiscountPercent = r.DiscountPercent; p.DiscountAmount = r.DiscountAmount; p.ExpiresAt = r.ExpiresAt;
        await db.SaveChangesAsync();
        return Ok(new { message = "updated" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePromo(Guid id)
    {
        var tid = User.GetTenantId();
        var p = await db.Promotions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tid && x.DeletedAt == null);
        if (p == null) return NotFound(new { message = "ไม่พบโปรโมชัน" });
        p.DeletedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        return Ok(new { message = "deleted" });
    }

    [HttpPost("coupon")]
    public async Task<IActionResult> CreateCoupon([FromBody] CouponRequest r)
    {
        var c = new Coupon { TenantId = User.GetTenantId(), Code = r.Code, Campaign = r.Campaign, DiscountPercent = r.DiscountPercent, DiscountAmount = r.DiscountAmount, ExpiresAt = r.ExpiresAt, Quota = r.Quota, IsActive = true };
        db.Coupons.Add(c); await db.SaveChangesAsync();
        return Ok(new { c.Id });
    }

    [HttpPut("coupon/{id:guid}")]
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] CouponRequest r)
    {
        var tid = User.GetTenantId();
        var c = await db.Coupons.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tid && x.DeletedAt == null);
        if (c == null) return NotFound(new { message = "ไม่พบคูปอง" });
        c.Code = r.Code; c.Campaign = r.Campaign; c.DiscountPercent = r.DiscountPercent; c.DiscountAmount = r.DiscountAmount; c.ExpiresAt = r.ExpiresAt; c.Quota = r.Quota;
        await db.SaveChangesAsync();
        return Ok(new { message = "updated" });
    }

    // GET /api/promotion/validate/{code} — ตรวจคูปอง/โปรโค้ดว่าใช้ได้ไหม (ใช้ตอน POS)
    [HttpGet("validate/{code}")]
    public async Task<IActionResult> Validate(string code)
    {
        var tid = User.GetTenantId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        code = (code ?? "").Trim().ToUpper();

        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.TenantId == tid && c.DeletedAt == null
            && c.Code.ToUpper() == code);
        if (coupon != null)
        {
            if (!coupon.IsActive) return BadRequest(new { message = "คูปองถูกปิดใช้งาน" });
            if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt < today) return BadRequest(new { message = "คูปองหมดอายุแล้ว" });
            if (coupon.Quota > 0 && coupon.UsedCount >= coupon.Quota) return BadRequest(new { message = "คูปองถูกใช้ครบโควต้าแล้ว" });
            return Ok(new { type = "coupon", code = coupon.Code, label = coupon.Campaign, coupon.DiscountPercent, coupon.DiscountAmount });
        }

        var promo = await db.Promotions.FirstOrDefaultAsync(p => p.TenantId == tid && p.DeletedAt == null
            && p.Code != null && p.Code.ToUpper() == code);
        if (promo != null)
        {
            if (!promo.IsActive) return BadRequest(new { message = "โปรโมชันถูกปิดใช้งาน" });
            if (promo.ExpiresAt.HasValue && promo.ExpiresAt < today) return BadRequest(new { message = "โปรโมชันหมดอายุแล้ว" });
            return Ok(new { type = "promo", code = promo.Code, label = promo.Title, promo.DiscountPercent, promo.DiscountAmount });
        }

        return NotFound(new { message = "ไม่พบรหัสนี้" });
    }

    // POST /api/promotion/redeem-coupon/{code} — นับการใช้คูปอง (+1) ตอนชำระเงินจริง
    [HttpPost("redeem-coupon/{code}")]
    public async Task<IActionResult> RedeemCoupon(string code)
    {
        var tid = User.GetTenantId();
        code = (code ?? "").Trim().ToUpper();
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.TenantId == tid && c.DeletedAt == null && c.Code.ToUpper() == code);
        if (coupon == null) return Ok(new { message = "ไม่ใช่คูปอง (อาจเป็นโปรโมชัน) — ข้าม" });
        coupon.UsedCount += 1;
        await db.SaveChangesAsync();
        return Ok(new { coupon.UsedCount });
    }
}

public record PromotionRequest(string Title, string? Description, string? Code, decimal? DiscountPercent, decimal? DiscountAmount, DateOnly? ExpiresAt);
public record CouponRequest(string Code, string? Campaign, decimal? DiscountPercent, decimal? DiscountAmount, DateOnly? ExpiresAt, int Quota);
