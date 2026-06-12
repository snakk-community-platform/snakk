namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("StatsRollup")]
public class StatsRollupDatabaseEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Kind of stat, e.g. "platform-stats", "trending-spaces", "trending-contributors",
    /// "top-spaces-today", "top-contributors-today", "latest-active-spaces",
    /// "latest-contributors", "top-spaces-period:{period}", "top-contributors-period:{period}"
    /// </summary>
    public required string StatKind { get; set; }

    /// <summary>
    /// 1-based rank within the stat kind (1 = top result).
    /// For platform-stats (single row), use Rank=1.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// JSON-serialized payload (the DTO that the use case returns)
    /// </summary>
    public required string PayloadJson { get; set; }

    /// <summary>
    /// When this row was computed by the worker (UTC)
    /// </summary>
    public required DateTime ComputedAt { get; set; }
}
