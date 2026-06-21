namespace Snakk.Domain.Json;

using System.Text.Json;
using System.Text.Json.Serialization;
using Snakk.Domain.ValueObjects;

public class DiscussionIdJsonConverter : JsonConverter<DiscussionId>
{
    public override DiscussionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DiscussionId.From(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DiscussionId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
