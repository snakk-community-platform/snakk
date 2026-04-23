using System.Text.Json;

namespace Snakk.Web.Helpers;

public static class LocaleLoader
{
    public static IReadOnlyDictionary<string, string> LoadFlat(string jsonPath)
    {
        using var stream = File.OpenRead(jsonPath);
        using var doc = JsonDocument.Parse(stream);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(doc.RootElement, prefix: "", dict);
        return dict;
    }

    private static void Walk(JsonElement element, string prefix, Dictionary<string, string> dict)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = prefix.Length == 0 ? prop.Name : $"{prefix}.{prop.Name}";
                    Walk(prop.Value, key, dict);
                }
                break;
            case JsonValueKind.String:
                dict[prefix] = element.GetString() ?? string.Empty;
                break;
            default:
                dict[prefix] = element.ToString();
                break;
        }
    }
}
