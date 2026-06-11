using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMS.Api.Attributes;
using MMS.Api.Extensions;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Infrastructure.Persistence;

namespace MMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomerController(AppDbContext db, MMS.Infrastructure.Persistence.Services.ActivityTimelineService timeline) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.CustomerView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var query = db.Customers
            .Where(c => c.TenantId == tenantId && c.BranchId == branchId && c.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                c.DisplayName.Contains(search) ||
                (c.Phone != null && c.Phone.Contains(search)));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.LastVisitAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id, c.DisplayName, c.Phone, c.AvatarUrl,
                c.TotalVisits, c.TotalSpent, c.LastVisitAt,
                c.PreferredTherapistId, c.Notes
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.CustomerView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = User.GetTenantId();
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && c.DeletedAt == null);

        if (customer == null) return NotFound(new { message = "Customer not found" });
        return Ok(customer);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CustomerCreate)]
    public async Task<IActionResult> Create([FromBody] CustomerRequest req)
    {
        var tenantId = User.GetTenantId();
        var branchId = User.GetBranchId();

        var customer = new Customer
        {
            TenantId = tenantId,
            BranchId = branchId,
            DisplayName = req.DisplayName,
            Phone = req.Phone,
            AvatarUrl = req.AvatarUrl,
            Notes = req.Notes,
            LineUserId = req.LineUserId,
            PreferredTherapistId = req.PreferredTherapistId
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await timeline.LogAsync("customer_created", "Customer", customer.Id,
            $"เพิ่มลูกค้าใหม่ · {customer.DisplayName}", customer.DisplayName);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, new { customer.Id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CustomerEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CustomerRequest req)
    {
        var tenantId = User.GetTenantId();
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && c.DeletedAt == null);

        if (customer == null) return NotFound(new { message = "Customer not found" });

        customer.DisplayName = req.DisplayName;
        customer.Phone = req.Phone;
        customer.AvatarUrl = req.AvatarUrl;
        customer.Notes = req.Notes;
        customer.PreferredTherapistId = req.PreferredTherapistId;

        await db.SaveChangesAsync();
        return Ok(new { message = "Updated" });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CustomerEdit)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = User.GetTenantId();
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && c.DeletedAt == null);

        if (customer == null) return NotFound(new { message = "Customer not found" });

        customer.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Deleted" });
    }
}

public record CustomerRequest(
    string DisplayName,
    string? Phone,
    string? AvatarUrl,
    string? Notes,
    string? LineUserId,
    Guid? PreferredTherapistId);
