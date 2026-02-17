namespace Snakk.Application.DTOs.Settings;

public class SettingDto
{
    public required string Id { get; set; }
    public required string Category { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public required string ValueType { get; set; }
    public bool IsEncrypted { get; set; }
    public string? Description { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUsername { get; set; }
}

public class SettingsByCategoryResponse
{
    public required string Category { get; set; }
    public required List<SettingDto> Settings { get; set; }
}
