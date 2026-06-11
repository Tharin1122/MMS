using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Extensions;
using MMS.Domain.Entities;
using MMS.Domain.Helper;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpenseController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? year, [FromQuery] int? month)
    {
        var tid = User.GetTenantId();
        var now = ThaiTime.Now;
        year ??= now.Year; month ??= now.Month;
        var from = new DateTime(year.Value, month.Value, 1).AddHours(-7);
        var to = new DateTime(year.Value, month.Value, DateTime.DaysInMonth(year.Value, month.Value), 23, 59, 59).AddHours(-7);
        var items = await db.Expenses.Where(e => e.TenantId == tid && e.DeletedAt == null && e.SpentAt >= from && e.SpentAt <= to)
            .OrderByDescending(e => e.SpentAt).ToListAsync();
        var byCategory = items.GroupBy(e => e.Category).Select(g => new { category = g.Key, amount = g.Sum(x => x.Amount), count = g.Count() }).OrderByDescending(g => g.amount).ToList();
        return Ok(new { total = items.Sum(e => e.Amount), byCategory, items });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ExpenseRequest r)
    {
        var e = new Expense { TenantId = User.GetTenantId(), BranchId = User.GetBranchId(), Category = r.Category, Note = r.Note, Amount = r.Amount, SpentAt = r.SpentAt ?? DateTime.UtcNow };
        db.Expenses.Add(e); await db.SaveChangesAsync();
        return Ok(new { e.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tid = User.GetTenantId();
        var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tid && x.DeletedAt == null);
        if (e == null) return NotFound(new { message = "ไม่พบรายการ" });
        e.DeletedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        return Ok(new { message = "deleted" });
    }
}

public record ExpenseRequest(string Category, string? Note, decimal Amount, DateTime? SpentAt);
