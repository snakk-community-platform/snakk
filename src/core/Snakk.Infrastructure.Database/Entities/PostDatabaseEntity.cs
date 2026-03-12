namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;

[Table("Post")]
public class PostDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Required attributes
    public required string Content { get; set; }
    public string RenderedContent { get; set; } = "";
    public required DateTime CreatedAt { get; set; }

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsFirstPost { get; set; }
    public bool HasCodeBlock { get; set; }
    public int RevisionCount { get; set; }
    public int ReactionCount { get; set; }

    // Denormalized post flags (computed at write time)
    public bool IsUsersFirstPostInDiscussion { get; set; }
    public bool IsUsersFirstPostInSpace { get; set; }
    public bool IsOp { get; set; }
    public bool IsNecro { get; set; }
    public bool IsMilestone { get; set; }

    // Many-to-one relationships
    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public int CreatedByUserId { get; set; }
    public virtual UserDatabaseEntity CreatedByUser { get; set; } = null!;

    // Full-text search vector (stored generated column)
    public NpgsqlTsVector SearchVector { get; set; } = null!;

    // Optional reply-to relationship (for threading)
    public int? ReplyToPostId { get; set; }
    public virtual PostDatabaseEntity? ReplyToPost { get; set; }
}
