namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeDebatePostPosition")]
public class DiscussionTypeDebatePostPositionDatabaseEntity
{
    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int PositionId { get; set; }
    public virtual DiscussionTypeDebatePositionDatabaseEntity Position { get; set; } = null!;
}
