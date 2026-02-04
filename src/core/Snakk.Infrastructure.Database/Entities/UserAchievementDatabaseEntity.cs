namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("UserAchievement")]
public class UserAchievementDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Required attributes
    public required DateTime EarnedAt { get; set; }

    // Optional attributes
    public bool IsDisplayed { get; set; }
    public int DisplayOrder { get; set; }
    public bool NotificationSent { get; set; }

    // Many-to-one relationships
    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public int AchievementId { get; set; }
    public virtual AchievementDatabaseEntity Achievement { get; set; } = null!;
}
