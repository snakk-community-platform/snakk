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
    public int SpaceCount { get; set; }
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }

    // Navigation properties
    public virtual CommunityDatabaseEntity Community { get; set; } = null!;
    public virtual ICollection<SpaceDatabaseEntity> Spaces { get; set; } = [];
}
