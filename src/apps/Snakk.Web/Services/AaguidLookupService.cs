namespace Snakk.Web.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

public class AaguidLookupService
{
    private readonly Dictionary<string, AaguidEntry> _map;

    public AaguidLookupService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.WebRootPath, "data", "aaguid.json");
        if (!File.Exists(path))
        {
            _map = [];
            return;
        }
        using var stream = File.OpenRead(path);
        _map = JsonSerializer.Deserialize(stream, AaguidJsonContext.Default.DictionaryStringAaguidEntry) ?? [];
    }

    public AaguidEntry? Lookup(Guid aaguid) =>
        aaguid == Guid.Empty ? null : _map.GetValueOrDefault(aaguid.ToString());
}

public record AaguidEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("icon_light")] string? IconLight,
    [property: JsonPropertyName("icon_dark")] string? IconDark);

[JsonSerializable(typeof(Dictionary<string, AaguidEntry>))]
internal partial class AaguidJsonContext : JsonSerializerContext { }
