namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

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

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public int PostCount { get; set; }
    public int ReactionCount { get; set; } // Unique users who reacted to any post in discussion

    // Tags (comma-separated for simplicity, e.g. "feature,bug,help")
    public string? Tags { get; set; }

    // Many-to-one relationships
    public int SpaceId { get; set; }
    public virtual SpaceDatabaseEntity Space { get; set; } = null!;

    public int CreatedByUserId { get; set; }
    public virtual UserDatabaseEntity CreatedByUser { get; set; } = null!;

    // One-to-many relationships
    public virtual ICollection<PostDatabaseEntity> Posts { get; set; } = [];
}
