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

        // สร้าง OTP 6 หลัก
        var otp = new Random().Next(100000, 999999).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        // ส่ง Push Message ไป LINE
        var message = new
        {
            type = "text",
            text = $"🔐 รหัส OTP ของคุณ: {otp}\nใช้ได้เพียง 10 นาที"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push")
        {
            Headers = { { "Authorization", $"Bearer {_channelAccessToken}" } },
            Content = new StringContent(JsonSerializer.Serialize(new { to = lineUserId, messages = new[] { message } }), Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return LineOtpResult.Fail("ส่ง OTP ไม่สำเร็จ — ตรวจสอบว่า user เพิ่ม LINE Official Account แล้ว");

        return LineOtpResult.Ok(otp, expiresAt);
    }
}

public record LineOtpResult(bool Success, string? Otp, DateTime? ExpiresAt, string? ErrorMessage)
{
    public static LineOtpResult Ok(string otp, DateTime expiresAt)
        => new(true, otp, expiresAt, null);

    public static LineOtpResult Fail(string error)
        => new(false, null, null, error);
}
