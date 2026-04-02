namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Space")]
public class SpaceDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public required string Slug { get; set; }

    // Required attributes
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Access control
    public bool AllowAnonymousReading { get; set; }
    public bool RequireEmailConfirmation { get; set; }
    public bool IsRestricted { get; set; }

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Avatar
    public string? AvatarFileName { get; set; }
    public int AvatarRevision { get; set; } = 0;

    // Rules denormalization
    public bool HasRules { get; set; }
    public string? RulesRevision { get; set; }
    public bool ParentHubHasRules { get; set; }
    public bool ParentCommunityHasRules { get; set; }

    // Team revision for moderator list cache-busting
    public string? TeamRevision { get; set; }

    // Denormalized counts for performance
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }
    public int ReactionCount { get; set; }

    // Many-to-one relationships
    public int HubId { get; set; }
    public virtual HubDatabaseEntity Hub { get; set; } = null!;

    // One-to-many relationships
    public virtual ICollection<DiscussionDatabaseEntity> Discussions { get; set; } = [];
    public virtual ICollection<RuleDatabaseEntity> Rules { get; set; } = [];
    public virtual ICollection<SpaceAllowedDiscussionTypeDatabaseEntity> AllowedDiscussionTypes { get; set; } = [];
    public virtual ICollection<GroupAccessDatabaseEntity> GroupAccess { get; set; } = [];
}
