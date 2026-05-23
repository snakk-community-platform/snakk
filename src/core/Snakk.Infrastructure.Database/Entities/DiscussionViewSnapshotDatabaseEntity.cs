namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionViewSnapshot")]
public class DiscussionViewSnapshotDatabaseEntity
{
    public int Id { get; set; }

    public required DateTime Hour { get; set; }

    public required string DiscussionPublicId { get; set; } = string.Empty;

    public required string CountryCode { get; set; } = string.Empty;

    public long ViewCount { get; set; }
}
