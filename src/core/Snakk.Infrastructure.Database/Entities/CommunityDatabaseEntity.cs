namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Community")]
public class CommunityDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public required string Slug { get; set; }

    // Required attributes
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Visibility and feed settings
    public int VisibilityId { get; set; } // Maps to CommunityVisibilityEnum
    public bool ExposeToPlatformFeed { get; set; }
    public bool IsAdultOnly { get; set; }
    public bool HideAdultDiscussionsFromLists { get; set; }

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Avatar
    public string? AvatarFileName { get; set; }
    public string? AvatarThumbnailFileName { get; set; }
    public string? AvatarMicroFileName { get; set; }
    public int AvatarRevision { get; set; } = 0;

    // Timezone (IANA timezone ID, e.g. "Europe/London"; null = use site-wide setting)
    public string? Timezone { get; set; }

    // Language (BCP 47 language tag, e.g. "en", "nb-NO"; null = use site-wide setting)
    public string? LanguageCode { get; set; }

    // Group access control
    public bool IsRestricted { get; set; }
    public bool Require2FA { get; set; }

    // Rules denormalization
    public bool HasRules { get; set; }
    public string? RulesRevision { get; set; }

    // Team revision for moderator list cache-busting
    public string? TeamRevision { get; set; }

    // Denormalized counts for performance
    public int HubCount { get; set; }
    public int SpaceCount { get; set; }
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }
    public int ReactionCount { get; set; }
    public long ViewCount { get; set; }

    // One-to-many relationships
    public virtual ICollection<HubDatabaseEntity> Hubs { get; set; } = [];
    public virtual ICollection<CommunityDomainDatabaseEntity> Domains { get; set; } = [];
    public virtual ICollection<RuleDatabaseEntity> Rules { get; set; } = [];
    public virtual ICollection<GroupDatabaseEntity> Groups { get; set; } = [];
    public virtual ICollection<GroupAccessDatabaseEntity> GroupAccess { get; set; } = [];
    public virtual ICollection<CommunityAllowedDiscussionTypeDatabaseEntity> AllowedDiscussionTypes { get; set; } = [];
}
