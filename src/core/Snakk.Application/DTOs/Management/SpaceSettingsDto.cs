using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Management;

public class SpaceSettingsDto
{
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public List<string> AllowedThreadTypes { get; set; } = new(); // e.g., "Discussion", "Question", "Announcement"

    public bool RequireApproval { get; set; }

    public bool AllowAnonymous { get; set; }

    public List<string> ModeratorUserIds { get; set; } = new();
}

public class UpdateSpaceSettingsRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public List<string> AllowedThreadTypes { get; set; } = new();

    public bool RequireApproval { get; set; }

    public bool AllowAnonymous { get; set; }
}
