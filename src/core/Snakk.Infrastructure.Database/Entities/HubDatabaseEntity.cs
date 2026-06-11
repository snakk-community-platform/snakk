namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Hub")]
public class HubDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public required string Slug { get; set; }

    // Foreign key to Community
    public int CommunityId { get; set; }
    public string? CommunityPublicId { get; set; }

    // Required attributes
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Access control
    public bool AllowAnonymousReading { get; set; }
    public bool RequireEmailConfirmation { get; set; }
    public bool IsRestricted { get; set; }
    public bool IsAdultOnly { get; set; }
    public bool Require2FA { get; set; }

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Avatar
    public string? AvatarFileName { get; set; }
    public string? AvatarThumbnailFileName { get; set; }
    public string? AvatarMicroFileName { get; set; }
    public int AvatarRevision { get; set; } = 0;

    // Language (BCP 47 language tag; null = inherit from community)
    public string? LanguageCode { get; set; }
    public string? CommunityLanguageCode { get; set; }

    // Rules denormalization
    public bool HasRules { get; set; }
    public string? RulesRevision { get; set; }
    public bool ParentCommunityHasRules { get; set; }

    // Team revision for moderator list cache-busting
    public string? TeamRevision { get; set; }

    // Denormalized counts for performance
    public int SpaceCount { get; set; }
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }
    public int ReactionCount { get; set; }

    // Navigation properties
    public virtual CommunityDatabaseEntity Community { get; set; } = null!;
    public virtual ICollection<SpaceDatabaseEntity> Spaces { get; set; } = [];
    public virtual ICollection<RuleDatabaseEntity> Rules { get; set; } = [];
    public virtual ICollection<GroupAccessDatabaseEntity> GroupAccess { get; set; } = [];
    public virtual ICollection<HubAllowedDiscussionTypeDatabaseEntity> AllowedDiscussionTypes { get; set; } = [];
}
