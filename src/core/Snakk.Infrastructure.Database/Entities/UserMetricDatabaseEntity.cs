namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("UserMetric")]
public class UserMetricDatabaseEntity
{
    // Composite key: UserId + MetricType + Scope + ScopeId
    public int UserId { get; set; }

    public required string MetricType { get; set; } // POST_COUNT, REACTION_GIVEN, etc.

    public required string Scope { get; set; } // GLOBAL, COMMUNITY, HUB, SPACE

    public int? ScopeId { get; set; } // NULL for GLOBAL, otherwise FK to Community/Hub/Space

    public int Value { get; set; }

    public required DateTime LastUpdated { get; set; }

    // Navigation properties
    public virtual UserDatabaseEntity User { get; set; } = null!;
}
