using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MMS.Infrastructure.Persistence.Auth;

public class LineService(IConfiguration config, HttpClient httpClient)
{
    private readonly string _channelId = config["Line:ChannelId"] ?? "";

    /// <summary>
    /// ยืนยัน LINE Access Token กับ LINE API
    /// คืน LineUserId ถ้า token ถูกต้อง
    /// </summary>
    public async Task<LineVerifyResult> VerifyAccessTokenAsync(string accessToken)
    {
        // เรียก LINE Verify API
        var response = await httpClient.GetAsync(
            $"https://api.line.me/oauth2/v2.1/verify?access_token={accessToken}");

        if (!response.IsSuccessStatusCode)
            return LineVerifyResult.Fail("LINE token ไม่ถูกต้องหรือหมดอายุ");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        // ตรวจ client_id ตรงกับ ChannelId ของเรา
        if (doc.RootElement.TryGetProperty("client_id", out var clientId))
        {
            if (clientId.GetString() != _channelId && !string.IsNullOrEmpty(_channelId))
                return LineVerifyResult.Fail("Token นี้ไม่ได้ออกให้แอปนี้");
        }

        // ดึง Profile (LineUserId, DisplayName, AvatarUrl)
        var profileResponse = await httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://api.line.me/v2/profile"),
            Headers = { { "Authorization", $"Bearer {accessToken}" } }
        });

        if (!profileResponse.IsSuccessStatusCode)
            return LineVerifyResult.Fail("ไม่สามารถดึงข้อมูล Profile ได้");

        var profileJson = await profileResponse.Content.ReadAsStringAsync();
        var profile = JsonDocument.Parse(profileJson).RootElement;

        return LineVerifyResult.Ok(
            lineUserId: profile.GetProperty("userId").GetString()!,
            displayName: profile.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
            avatarUrl: profile.TryGetProperty("pictureUrl", out var pic) ? pic.GetString() : null
        );
    }
}

public record LineVerifyResult(
    bool Success,
    string? LineUserId,
    string? DisplayName,
    string? AvatarUrl,
    string? ErrorMessage)
{
    public static LineVerifyResult Ok(string lineUserId, string? displayName, string? avatarUrl)
        => new(true, lineUserId, displayName, avatarUrl, null);

    public static LineVerifyResult Fail(string error)
        => new(false, null, null, null, error);
}
