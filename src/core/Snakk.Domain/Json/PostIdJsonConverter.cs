namespace Snakk.Domain.Json;

using System.Text.Json;
using System.Text.Json.Serialization;
using Snakk.Domain.ValueObjects;

public class PostIdJsonConverter : JsonConverter<PostId>
{
    public override PostId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        PostId.From(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, PostId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
