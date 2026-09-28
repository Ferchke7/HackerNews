using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackerNews.Common.Json;

public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value is null ? default : DateOnly.ParseExact(value, Format);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format));
    }
}
