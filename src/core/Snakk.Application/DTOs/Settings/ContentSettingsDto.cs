namespace Snakk.Application.DTOs.Settings;

public class ContentSettingsDto
{
    public int PostMaxLength { get; set; } = 50000;
    public int DiscussionTitleMaxLength { get; set; } = 300;
    public bool AllowMarkdown { get; set; } = true;
    public bool AllowHtml { get; set; } = false;
    public bool RequireModeration { get; set; } = false;
    // Script names as strings for JSON-friendly serialization (e.g. "Latin", "Cyrillic")
    public List<string> AllowedDisplayNameScripts { get; set; } = ["Latin"];
}
