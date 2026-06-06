using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MMS.Api.Extensions;

namespace MMS.Api.Hubs;

/// <summary>
/// MMS Realtime Hub — ทุก client เชื่อมต่อที่นี่
/// Group strategy: "branch_{branchId}" — broadcast เฉพาะคนในสาขาเดียวกัน
/// </summary>
[Authorize]
public class MmsHub : Hub
{
    /// <summary>
    /// Client เชื่อมต่อสำเร็จ — เข้า group ของสาขาตัวเองอัตโนมัติ
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var branchId = Context.User?.GetBranchId();
        var tenantId = Context.User?.GetTenantId();
        var userId = Context.User?.GetUserId();

        if (branchId.HasValue)
        {
            // เข้า group ของสาขา
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GroupName.Branch(branchId.Value));

            // เข้า group ของ tenant ด้วย (สำหรับ Owner ดูทุกสาขา)
            if (tenantId.HasValue)
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    GroupName.Tenant(tenantId.Value));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Client disconnect — ออกจาก group อัตโนมัติ (SignalR จัดการให้)
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client ping — ทดสอบ connection
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", new
        {
            message = "pong",
            serverTime = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Group name helpers — ใช้แทน string เพื่อป้องกัน typo
/// </summary>
public static class GroupName
{
    public static string Branch(Guid branchId) => $"branch_{branchId}";
    public static string Tenant(Guid tenantId) => $"tenant_{tenantId}";
}