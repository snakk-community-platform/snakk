namespace Snakk.Domain.Json;

using System.Text.Json;
using System.Text.Json.Serialization;
using Snakk.Domain.ValueObjects;

public class UserIdJsonConverter : JsonConverter<UserId>
{
    public override UserId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UserId.From(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, UserId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
