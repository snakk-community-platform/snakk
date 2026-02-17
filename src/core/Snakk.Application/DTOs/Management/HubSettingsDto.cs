using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Management;

public class HubSettingsDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public List<string> ModeratorUserIds { get; set; } = new();
}

public class UpdateHubSettingsRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
