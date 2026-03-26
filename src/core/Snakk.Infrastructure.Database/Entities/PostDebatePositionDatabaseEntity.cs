namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("PostDebatePosition")]
public class PostDebatePositionDatabaseEntity
{
    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int PositionId { get; set; }
    public virtual DiscussionDebatePositionDatabaseEntity Position { get; set; } = null!;
}
