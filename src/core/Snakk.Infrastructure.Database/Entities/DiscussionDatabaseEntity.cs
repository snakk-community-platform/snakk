namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;

[Table("Discussion")]
public class DiscussionDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public required string Slug { get; set; }

    // Required attributes
    public required string Title { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Discussion type
    public int Type { get; set; } // Maps to DiscussionTypeEnum

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsAdultOnly { get; set; }
    public bool WasNormalized { get; set; }
    public int PostCount { get; set; }
    public int ReactionCount { get; set; } // Total reactions across all posts in discussion
    public int FollowerCount { get; set; }
    public double TrendScore { get; set; }

    // Denormalized OP author display fields (cascaded on user rename/avatar change)
    public string? AuthorDisplayName { get; set; }
    public string? AuthorAvatarFileName { get; set; }
    public string? AuthorAvatarThumbnailFileName { get; set; }

    // Denormalized last-reply preview (updated on post create/delete)
    public string? LastPostAuthorPublicId { get; set; }
    public string? LastPostAuthorDisplayName { get; set; }
    public string? LastPostAuthorAvatarFileName { get; set; }
    public string? LastPostAuthorAvatarThumbnailFileName { get; set; }
    [MaxLength(200)]
    public string? LastPostPlainTextExcerpt { get; set; }

    // Tags (comma-separated for simplicity, e.g. "feature,bug,help")
    public string? Tags { get; set; }

    // Many-to-one relationships
    public int SpaceId { get; set; }
    public string? SpacePublicId { get; set; }
    public int HubId { get; set; }
    public string? HubPublicId { get; set; }
    public int CommunityId { get; set; }
    public string? CommunityPublicId { get; set; }
    public virtual SpaceDatabaseEntity Space { get; set; } = null!;

    public int CreatedByUserId { get; set; }
    public string? CreatedByUserPublicId { get; set; }
    public virtual UserDatabaseEntity CreatedByUser { get; set; } = null!;

    // Full-text search vector (stored generated column)
    public NpgsqlTsVector SearchVector { get; set; } = null!;

    // One-to-many relationships
    public virtual ICollection<PostDatabaseEntity> Posts { get; set; } = [];
}
