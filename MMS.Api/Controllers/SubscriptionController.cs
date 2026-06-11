using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMS.Api.Extensions;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionController(AppDbContext db) : ControllerBase
{
    private static readonly string[] Plans = { "Free", "Starter", "Professional", "Enterprise" };

    // GET /api/subscription — แพ็กเกจปัจจุบันของร้าน
    [HttpGet]
    public async Task<IActionResult> Current()
    {
        var tenant = await db.Tenants.FindAsync(User.GetTenantId());
        if (tenant == null) return NotFound();
        return Ok(new { planType = tenant.PlanType, trialEndsAt = tenant.TrialEndsAt, status = tenant.Status.ToString() });
    }

    public record SelectPlanRequest(string PlanType, bool Trial = false);

    // POST /api/subscription/select — เลือก/อัปเกรดแพ็กเกจ (เปลี่ยน PlanType จริงใน DB)
    [HttpPost("select")]
    public async Task<IActionResult> Select([FromBody] SelectPlanRequest r)
    {
        var plan = Plans.FirstOrDefault(p => p.Equals(r.PlanType, StringComparison.OrdinalIgnoreCase));
        if (plan == null) return BadRequest(new { message = "ไม่พบแพ็กเกจนี้" });

        var tenant = await db.Tenants.FindAsync(User.GetTenantId());
        if (tenant == null) return NotFound();

        tenant.PlanType = plan;
        tenant.TrialEndsAt = r.Trial ? DateTime.UtcNow.AddDays(14) : null;
        await db.SaveChangesAsync();

        return Ok(new { message = r.Trial ? "เริ่มทดลองใช้ 14 วันแล้ว" : "เปลี่ยนเป็นแพ็กเกจ " + plan + " แล้ว", planType = tenant.PlanType, trialEndsAt = tenant.TrialEndsAt });
    }
}
