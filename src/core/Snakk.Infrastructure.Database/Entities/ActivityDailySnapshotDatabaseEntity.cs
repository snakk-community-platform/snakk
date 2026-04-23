namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;
using Snakk.Shared.Enums;

[Table("ActivityDailySnapshot")]
public class ActivityDailySnapshotDatabaseEntity
{
    public int Id { get; set; }

    public required DateOnly Date { get; set; }

    public required ActivityEntityTypeEnum EntityType { get; set; }

    /// <summary>
    /// Integer primary key of the referenced entity. Zero for platform-level rows.
    /// </summary>
    public int EntityId { get; set; }

    public int PostCount { get; set; }

    public int DiscussionCount { get; set; }
}
