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
public class ReviewController(AppDbContext db) : ControllerBase
{
    public record ReviewRequest(Guid CustomerId, Guid? TherapistId, Guid? PaymentId, int Rating, string? Comment);

    // POST /api/review — ลูกค้าให้คะแนน
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReviewRequest r)
    {
        if (r.Rating < 1 || r.Rating > 5) return BadRequest(new { message = "คะแนนต้อง 1-5 ดาว" });
        var rv = new Review
        {
            TenantId = User.GetTenantId(), BranchId = User.GetBranchId(),
            CustomerId = r.CustomerId, TherapistId = r.TherapistId, PaymentId = r.PaymentId,
            Rating = r.Rating, Comment = r.Comment,
        };
        db.Reviews.Add(rv);
        await db.SaveChangesAsync();
        return Ok(new { rv.Id });
    }

    // GET /api/review — รายการรีวิวล่าสุด + ค่าเฉลี่ยรวม
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tid = User.GetTenantId();
        var q = db.Reviews.Where(r => r.TenantId == tid && r.DeletedAt == null);
        var all = await q
            .OrderByDescending(r => r.CreatedAt).Take(50)
            .Select(r => new
            {
                r.Id, r.Rating, r.Comment, r.CreatedAt,
                customer = r.Customer.DisplayName,
                therapist = r.Therapist != null ? r.Therapist.DisplayName : null,
            }).ToListAsync();
        var avg = await q.AnyAsync() ? Math.Round(await q.AverageAsync(r => (double)r.Rating), 2) : 0;
        var count = await q.CountAsync();
        return Ok(new { average = avg, count, items = all });
    }

    // GET /api/review/therapists — คะแนนเฉลี่ยรายหมอนวด (ใช้ในหน้าพนักงาน/รายงาน)
    [HttpGet("therapists")]
    public async Task<IActionResult> ByTherapist()
    {
        var tid = User.GetTenantId();
        var rows = await db.Reviews
            .Where(r => r.TenantId == tid && r.DeletedAt == null && r.TherapistId != null)
            .GroupBy(r => r.TherapistId)
            .Select(g => new { therapistId = g.Key, average = Math.Round(g.Average(x => (double)x.Rating), 2), count = g.Count() })
            .ToListAsync();
        return Ok(rows);
    }
}
