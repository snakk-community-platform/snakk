namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Achievement")]
public class AchievementDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public required string Slug { get; set; }

    // Required attributes
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Optional attributes
    public string? IconUrl { get; set; }
    public int TierLevel { get; set; }
    public int Points { get; set; }
    public bool IsSecret { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // JSON configuration (will be JSONB in PostgreSQL)
    public required string RequirementConfig { get; set; }

    // Many-to-one relationships
    public int CategoryId { get; set; }
    public virtual Lookups.AchievementCategoryLookup Category { get; set; } = null!;

    public int RequirementTypeId { get; set; }
    public virtual Lookups.AchievementRequirementTypeLookup RequirementType { get; set; } = null!;

    // One-to-many relationships
    public virtual ICollection<UserAchievementDatabaseEntity> UserAchievements { get; set; } = [];
    public virtual ICollection<UserAchievementProgressDatabaseEntity> UserAchievementProgress { get; set; } = [];
}
