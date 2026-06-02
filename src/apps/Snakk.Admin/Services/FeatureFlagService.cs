namespace Snakk.Admin.Services;

using System.Text.Json;
using System.Text.Json.Nodes;

public class FeatureFlagService(IConfiguration configuration)
{
    public bool PrivateMessagingEnabled =>
        configuration.GetValue("Features:PrivateMessagingEnabled", defaultValue: true);

    public async Task SetPrivateMessagingEnabledAsync(bool value) =>
        await PatchFeatureFlagAsync("PrivateMessagingEnabled", value);

    private async Task PatchFeatureFlagAsync(string key, bool value)
    {
        var storagePath = configuration["FileStorage:BasePath"] ?? "/app/storage";
        var configPath = Path.Combine(storagePath, "conf", "snakk-config.json");

        JsonObject root;
        if (File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath);
            root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        if (root["Features"] is JsonObject features)
            features[key] = value;
        else
            root["Features"] = new JsonObject { [key] = value };

        await File.WriteAllTextAsync(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
