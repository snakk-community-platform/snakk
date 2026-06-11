using System.ComponentModel.DataAnnotations;
using Snakk.Shared.Enums;

namespace Snakk.Application.DTOs.Management;

public class CommunitySettingsDto
{
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public string? AvatarUrl { get; set; }

    public string? CustomDomain { get; set; }

    public bool IsPrivate { get; set; }

    public bool RequireApproval { get; set; }

    public bool AllowMemberInvites { get; set; } = true;

    public string? Timezone { get; set; }

    public string? LanguageCode { get; init; }

    public List<DiscussionTypeEnum> AllowedDiscussionTypes { get; set; } = [];

    public bool HideAdultDiscussionsFromLists { get; set; }

    public bool Require2FA { get; set; }

    // Owner and team
    public string OwnerId { get; set; } = string.Empty;
    public List<string> AdminUserIds { get; set; } = new();
    public List<string> ModeratorUserIds { get; set; } = new();
}

public class UpdateCommunitySettingsRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public string? AvatarUrl { get; set; }

    public string? CustomDomain { get; set; }

    public bool IsPrivate { get; set; }

    public bool RequireApproval { get; set; }

    public bool AllowMemberInvites { get; set; } = true;

    public string? Timezone { get; set; }

    public string? LanguageCode { get; init; }

    public List<DiscussionTypeEnum> AllowedDiscussionTypes { get; set; } = [];

    public bool HideAdultDiscussionsFromLists { get; set; }

    public bool Require2FA { get; set; }
}
