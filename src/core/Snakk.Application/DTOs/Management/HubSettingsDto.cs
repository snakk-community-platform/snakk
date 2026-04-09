using System.ComponentModel.DataAnnotations;
using Snakk.Shared.Enums;

namespace Snakk.Application.DTOs.Management;

public class HubSettingsDto
{
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public List<DiscussionTypeEnum> AllowedDiscussionTypes { get; set; } = [];

    public string? LanguageCode { get; init; }

    public string? CommunityLanguageCode { get; init; }

    public List<string> ModeratorUserIds { get; set; } = new();
}

public class UpdateHubSettingsRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public List<DiscussionTypeEnum> AllowedDiscussionTypes { get; set; } = [];

    public string? LanguageCode { get; init; }
}
