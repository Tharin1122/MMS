using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

// ─────────────────────────────────────────────
// Service Category
// ─────────────────────────────────────────────
[ApiController]
[Route("api/service-categories")]
[Authorize]
public class ServiceCategoryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ServiceView)]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = User.GetTenantId();
        var items = await db.ServiceCategories
            .Where(c => c.TenantId == tenantId && c.DeletedAt == null)
            .OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Name, c.SortOrder, c.IsActive })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ServiceCreate)]
    public async Task<IActionResult> Create([FromBody] ServiceCategoryRequest req)
    {
        var tenantId = User.GetTenantId();
        var category = new ServiceCategory
        {
            TenantId = tenantId,
            Name = req.Name,
            SortOrder = req.SortOrder
        };

        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();
        return Ok(new { category.Id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.ServiceEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ServiceCategoryRequest req)
    {
        var tenantId = User.GetTenantId();
        var category = await db.ServiceCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && c.DeletedAt == null);

        if (category == null) return NotFound(new { message = "Category not found" });

        category.Name = req.Name;
        category.SortOrder = req.SortOrder;
        category.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return Ok(new { message = "Updated" });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.ServiceDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var category = await db.ServiceCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && c.DeletedAt == null);

        if (category == null) return NotFound(new { message = "Category not found" });

        var hasServices = await db.Services
            .AnyAsync(s => s.CategoryId == id && s.DeletedAt == null);

        if (hasServices)
            return BadRequest(new { message = "Cannot delete category that still has services" });

        category.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }
}

// ─────────────────────────────────────────────
// Service
// ─────────────────────────────────────────────
[ApiController]
[Route("api/services")]
[Authorize]
public class ServiceController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ServiceView)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? categoryId, [FromQuery] bool? activeOnly)
    {
        var tenantId = User.GetTenantId();

        var query = db.Services
            .Include(s => s.Category)
            .Where(s => s.TenantId == tenantId && s.DeletedAt == null);

        if (categoryId.HasValue)
            query = query.Where(s => s.CategoryId == categoryId);

        if (activeOnly == true)
            query = query.Where(s => s.IsActive);

        var items = await query
            .OrderBy(s => s.Category.SortOrder)
            .ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Id, s.Name, s.DurationMins, s.BufferMins,
                s.Price, s.CommissionRate, s.CommissionFixed,
                s.RequiredRoomType, s.IsActive,
                Category = new { s.Category.Id, s.Category.Name }
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.ServiceView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var service = await db.Services
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.DeletedAt == null);

        if (service == null) return NotFound(new { message = "Service not found" });
        return Ok(service);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ServiceCreate)]
    public async Task<IActionResult> Create([FromBody] ServiceRequest req)
    {
        var tenantId = User.GetTenantId();

        var category = await db.ServiceCategories
            .FirstOrDefaultAsync(c => c.Id == req.CategoryId && c.TenantId == tenantId);
        if (category == null)
            return BadRequest(new { message = "Category not found" });

        var service = new Service
        {
            TenantId = tenantId,
            BranchId = req.BranchId,
            CategoryId = req.CategoryId,
            Name = req.Name,
            DurationMins = req.DurationMins,
            BufferMins = req.BufferMins,
            Price = req.Price,
            RequiredRoomType = req.RequiredRoomType,
            CommissionRate = req.CommissionRate,
            CommissionFixed = req.CommissionFixed
        };

        db.Services.Add(service);
        await db.SaveChangesAsync();
        return Ok(new { service.Id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.ServiceEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ServiceRequest req)
    {
        var tenantId = User.GetTenantId();
        var service = await db.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.DeletedAt == null);

        if (service == null) return NotFound(new { message = "Service not found" });

        service.CategoryId = req.CategoryId;
        service.Name = req.Name;
        service.DurationMins = req.DurationMins;
        service.BufferMins = req.BufferMins;
        service.Price = req.Price;
        service.RequiredRoomType = req.RequiredRoomType;
        service.CommissionRate = req.CommissionRate;
        service.CommissionFixed = req.CommissionFixed;
        service.IsActive = req.IsActive;

        await db.SaveChangesAsync();
        return Ok(new { message = "Updated" });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.ServiceDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var service = await db.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.DeletedAt == null);

        if (service == null) return NotFound(new { message = "Service not found" });

        service.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }
}

public record ServiceCategoryRequest(string Name, int SortOrder, bool IsActive = true);

public record ServiceRequest(
    Guid CategoryId,
    string Name,
    int DurationMins,
    int BufferMins,
    decimal Price,
    string? RequiredRoomType,
    decimal? CommissionRate,
    decimal? CommissionFixed,
    Guid? BranchId,
    bool IsActive = true);
