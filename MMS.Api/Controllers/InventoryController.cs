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
public class InventoryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? category)
    {
        var tenantId = User.GetTenantId();
        var q = db.InventoryItems.Where(i => i.TenantId == tenantId && i.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(i => i.Name.Contains(search) || (i.Sku != null && i.Sku.Contains(search)));
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(i => i.Category == category);
        var items = await q.OrderBy(i => i.Name).ToListAsync();
        var summary = new
        {
            totalItems = items.Count,
            totalValue = items.Sum(i => i.CostPerUnit * i.Quantity),
            lowStock = items.Count(i => i.Quantity <= i.ReorderPoint),
        };
        return Ok(new { summary, items });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] InventoryItemRequest r)
    {
        var item = new InventoryItem
        {
            TenantId = User.GetTenantId(), BranchId = User.GetBranchId(),
            Name = r.Name, Sku = r.Sku, Category = r.Category, Unit = r.Unit ?? "ชิ้น",
            CostPerUnit = r.CostPerUnit, Quantity = r.Quantity, ReorderPoint = r.ReorderPoint,
        };
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(new { item.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] InventoryItemRequest r)
    {
        var tenantId = User.GetTenantId();
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && i.DeletedAt == null);
        if (item == null) return NotFound(new { message = "ไม่พบสินค้า" });
        item.Name = r.Name; item.Sku = r.Sku; item.Category = r.Category; item.Unit = r.Unit ?? item.Unit;
        item.CostPerUnit = r.CostPerUnit; item.ReorderPoint = r.ReorderPoint;
        await db.SaveChangesAsync();
        return Ok(new { message = "updated" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && i.DeletedAt == null);
        if (item == null) return NotFound(new { message = "ไม่พบสินค้า" });
        item.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "deleted" });
    }

    // รับเข้า / เบิกใช้
    [HttpPost("{id:guid}/movement")]
    public async Task<IActionResult> Move(Guid id, [FromBody] StockMoveRequest r)
    {
        var tenantId = User.GetTenantId();
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && i.DeletedAt == null);
        if (item == null) return NotFound(new { message = "ไม่พบสินค้า" });
        var qty = Math.Abs(r.Quantity);
        if (r.Type == "out")
        {
            if (item.Quantity < qty) return BadRequest(new { message = "สต็อกไม่พอ (คงเหลือ " + item.Quantity + ")" });
            item.Quantity -= qty;
        }
        else item.Quantity += qty;
        db.StockMovements.Add(new StockMovement { TenantId = tenantId, InventoryItemId = id, Type = r.Type, Quantity = qty, Note = r.Note });
        await db.SaveChangesAsync();
        return Ok(new { item.Quantity });
    }

    [HttpGet("{id:guid}/movements")]
    public async Task<IActionResult> Movements(Guid id)
    {
        var tenantId = User.GetTenantId();
        var list = await db.StockMovements.Where(m => m.InventoryItemId == id && m.TenantId == tenantId && m.DeletedAt == null)
            .OrderByDescending(m => m.MovedAt).Take(50).ToListAsync();
        return Ok(list);
    }
}

public record InventoryItemRequest(string Name, string? Sku, string? Category, string? Unit, decimal CostPerUnit, int Quantity, int ReorderPoint);
public record StockMoveRequest(string Type, int Quantity, string? Note);
