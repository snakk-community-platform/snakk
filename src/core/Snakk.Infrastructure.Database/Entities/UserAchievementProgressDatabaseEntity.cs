namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("UserAchievementProgress")]
public class UserAchievementProgressDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }

    // Required attributes
    public int CurrentValue { get; set; }
    public int TargetValue { get; set; }
    public required DateTime LastUpdated { get; set; }

    // Optional attributes
    public string? ProgressData { get; set; } // JSONB for additional tracking data

    // Many-to-one relationships
    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public int AchievementId { get; set; }
    public virtual AchievementDatabaseEntity Achievement { get; set; } = null!;
}
