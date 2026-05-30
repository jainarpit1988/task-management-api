using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManagement.Application.Helpers;

namespace TaskManagement.Api.Serialization;

public sealed class IstDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return default;

        return IndiaDateTime.ParseToUtc(text);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var ist = IndiaDateTime.FromUtcToIst(value);
        var offset = new DateTimeOffset(ist, IndiaDateTime.IstOffset);
        writer.WriteStringValue(offset.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture));
    }
}

public sealed class IstNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return IndiaDateTime.ParseToUtc(text);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        var ist = IndiaDateTime.FromUtcToIst(value.Value);
        var offset = new DateTimeOffset(ist, IndiaDateTime.IstOffset);
        writer.WriteStringValue(offset.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture));
    }
}
