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
    public int VisibilityId { get; set; }
    public virtual Lookups.CommunityVisibilityLookup Visibility { get; set; } = null!;
    public bool ExposeToPlatformFeed { get; set; }

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Denormalized counts for performance
    public int HubCount { get; set; }
    public int SpaceCount { get; set; }
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }

    // One-to-many relationships
    public virtual ICollection<HubDatabaseEntity> Hubs { get; set; } = [];
    public virtual ICollection<CommunityDomainDatabaseEntity> Domains { get; set; } = [];
}
