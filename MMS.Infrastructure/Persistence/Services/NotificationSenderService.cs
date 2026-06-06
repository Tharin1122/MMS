using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MMS.Domain.Enums;
using System.Net.Http.Json;

namespace MMS.Infrastructure.Persistence.Services;

/// <summary>
/// Job ที่ Hangfire เรียก — ดึง NotificationQueue ที่ Pending แล้วส่ง
/// </summary>
public class NotificationSenderService(
    AppDbContext db,
    IConfiguration config,
    ILogger<NotificationSenderService> logger,
    HttpClient httpClient)
{
    private readonly string _lineToken = config["Line:ChannelAccessToken"] ?? "";

    /// <summary>
    /// Hangfire จะเรียก method นี้ทุก 1 นาที
    /// </summary>
    public async Task ProcessPendingAsync()
    {
        var now = DateTime.UtcNow;

        var pending = await db.NotificationQueues
            .Where(n => n.Status == NotificationStatus.Pending
                     && n.ScheduledAt <= now
                     && n.RetryCount < 3
                     && n.DeletedAt == null)
            .OrderBy(n => n.ScheduledAt)
            .Take(50)
            .ToListAsync();

        foreach (var noti in pending)
        {
            try
            {
                if (noti.Channel == NotificationChannel.Line)
                    await SendLineAsync(noti.LineUserId!, noti.Message);

                noti.Status = NotificationStatus.Sent;
                noti.SentAt = DateTime.UtcNow;
                logger.LogInformation("Sent {EventType} to {LineUserId}", noti.EventType, noti.LineUserId);
            }
            catch (Exception ex)
            {
                noti.RetryCount++;
                noti.ErrorMessage = ex.Message;

                // ครบ 3 ครั้งแล้วยัง fail → mark Failed
                if (noti.RetryCount >= 3)
                    noti.Status = NotificationStatus.Failed;

                logger.LogWarning(ex, "Failed to send notification {Id} (retry {Count})", noti.Id, noti.RetryCount);
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task SendLineAsync(string lineUserId, string message)
    {
        var body = new
        {
            to = lineUserId,
            messages = new[] { new { type = "text", text = message } }
        };

        var req = new HttpRequestMessage(HttpMethod.Post,
            "https://api.line.me/v2/bot/message/push")
        {
            Content = JsonContent.Create(body),
            Headers = { { "Authorization", $"Bearer {_lineToken}" } }
        };

        var resp = await httpClient.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }
}