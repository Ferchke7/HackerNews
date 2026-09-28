using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackerNews.Common.Json;

public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public const string Format = "HH:mm:ss";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value is null ? default : TimeOnly.ParseExact(value, Format);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format));
    }
}
