using System.Text.Json;
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
public class SettingsController(AppDbContext db) : ControllerBase
{
    // GET /api/settings → คืนค่า JSON การตั้งค่าของร้าน (หรือ {} ถ้ายังไม่เคยตั้ง)
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var tenantId = User.GetTenantId();
        var s = await db.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.DeletedAt == null);
        return Content(string.IsNullOrWhiteSpace(s?.SettingsJson) ? "{}" : s!.SettingsJson,
            "application/json; charset=utf-8");
    }

    // PUT /api/settings → บันทึกค่า JSON การตั้งค่า (ทั้งก้อน)
    [HttpPut]
    public async Task<IActionResult> Put([FromBody] JsonElement body)
    {
        var tenantId = User.GetTenantId();
        var json = body.GetRawText();

        var s = await db.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.DeletedAt == null);

        if (s == null)
        {
            s = new TenantSetting { TenantId = tenantId, SettingsJson = json };
            db.TenantSettings.Add(s);
        }
        else
        {
            s.SettingsJson = json;
            s.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "บันทึกการตั้งค่าแล้ว" });
    }
}
