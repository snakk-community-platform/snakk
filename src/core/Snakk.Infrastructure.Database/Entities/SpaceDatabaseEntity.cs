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

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Denormalized counts for performance
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }

    // Many-to-one relationships
    public int HubId { get; set; }
    public virtual HubDatabaseEntity Hub { get; set; } = null!;

    // One-to-many relationships
    public virtual ICollection<DiscussionDatabaseEntity> Discussions { get; set; } = [];
}
