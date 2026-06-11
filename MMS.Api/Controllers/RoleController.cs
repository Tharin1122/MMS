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
public class RoleController(AppDbContext db) : ControllerBase
{
    private const string MatrixKey = "__roleMatrix";

    // GET /api/role/matrix → คืน matrix สิทธิ์ของแต่ละบทบาท (level 0/1/2 ต่อโมดูล) จาก DB
    [HttpGet("matrix")]
    public async Task<IActionResult> GetMatrix()
    {
        var tenantId = User.GetTenantId();
        var s = await db.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(s?.SettingsJson))
        {
            using var doc = JsonDocument.Parse(s!.SettingsJson);
            if (doc.RootElement.TryGetProperty(MatrixKey, out var m))
                return Content(m.GetRawText(), "application/json; charset=utf-8");
        }
        return Content("null", "application/json; charset=utf-8");
    }

    // PUT /api/role/matrix → บันทึก matrix สิทธิ์ลง DB (merge เข้า settings blob เดิม ไม่ทับค่าอื่น)
    [HttpPut("matrix")]
    public async Task<IActionResult> PutMatrix([FromBody] JsonElement body)
    {
        var tenantId = User.GetTenantId();

        var s = await db.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.DeletedAt == null);

        var dict = new Dictionary<string, JsonElement>();
        if (!string.IsNullOrWhiteSpace(s?.SettingsJson))
        {
            using var doc = JsonDocument.Parse(s!.SettingsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject())
                    dict[p.Name] = p.Value.Clone();
        }
        dict[MatrixKey] = body.Clone();
        var json = JsonSerializer.Serialize(dict);

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
        return Ok(new { message = "บันทึกสิทธิ์การเข้าถึงแล้ว" });
    }
}
