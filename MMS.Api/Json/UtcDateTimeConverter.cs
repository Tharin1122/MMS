using System.Text.Json;
using System.Text.Json.Serialization;

namespace MMS.Api.Json;

/// <summary>
/// ส่ง DateTime ออกเป็น UTC ISO (มี 'Z') เสมอ เพื่อให้ frontend แปลงเป็นเวลาท้องถิ่นได้ถูกต้อง
/// DB เก็บเป็น UTC → ติด label Utc แล้ว serialize เป็น ...Z
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions o)
        => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
}

public class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions o)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}
