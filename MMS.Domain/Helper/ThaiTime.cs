namespace MMS.Domain.Helper;
public static class ThaiTime
{
    private static readonly TimeZoneInfo _tz =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);

    public static DateOnly Today =>
        DateOnly.FromDateTime(Now);

    public static DateTime FromUtc(DateTime utc) => utc.AddHours(7);
}
