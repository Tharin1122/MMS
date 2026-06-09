using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MMS.Infrastructure.Persistence.Auth;

/// <summary>
/// ส่ง OTP ผ่าน LINE Bot API (ใช้ Push Message API)
/// </summary>
public class LineOtpService(IConfiguration config, HttpClient httpClient)
{
    private readonly string _channelAccessToken = config["Line:ChannelAccessToken"] ?? "";

    /// <summary>
    /// ส่ง OTP ไปที่ LINE user แล้วเก็บ OTP ไว้ใน code
    /// </summary>
    public async Task<LineOtpResult> SendOtpAsync(string lineUserId)
    {
        if (string.IsNullOrEmpty(_channelAccessToken))
            return LineOtpResult.Fail("LINE Bot Token ยังไม่ได้ตั้งค่า");

        var otp = new Random().Next(100000, 999999).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        var ok = await SendTextAsync(lineUserId, $"🔐 รหัส OTP ของคุณ: {otp}\nใช้ได้เพียง 10 นาที");
        if (!ok)
            return LineOtpResult.Fail("ส่ง OTP ไม่สำเร็จ — ตรวจสอบว่า user เพิ่ม LINE Official Account แล้ว");

        return LineOtpResult.Ok(otp, expiresAt);
    }

    /// <summary>
    /// ส่งข้อความ text ไปยัง LINE user คนเดียว (Push Message)
    /// </summary>
    public async Task<bool> SendTextAsync(string lineUserId, string text)
    {
        if (string.IsNullOrEmpty(_channelAccessToken) || string.IsNullOrEmpty(lineUserId))
            return false;

        var message = new { type = "text", text };
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push")
        {
            Headers = { { "Authorization", $"Bearer {_channelAccessToken}" } },
            Content = new StringContent(
                JsonSerializer.Serialize(new { to = lineUserId, messages = new[] { message } }),
                Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public record LineOtpResult(bool Success, string? Otp, DateTime? ExpiresAt, string? ErrorMessage)
{
    public static LineOtpResult Ok(string otp, DateTime expiresAt)
        => new(true, otp, expiresAt, null);

    public static LineOtpResult Fail(string error)
        => new(false, null, null, error);
}
