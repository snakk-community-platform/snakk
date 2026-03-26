namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionDebatePosition")]
public class DiscussionDebatePositionDatabaseEntity
{
    public int Id { get; set; }

    public int DebateId { get; set; }
    public virtual DiscussionDebateDatabaseEntity Debate { get; set; } = null!;

    public int Index { get; set; } // 0, 1, or 2

    [MaxLength(100)]
    public required string Label { get; set; }
}
