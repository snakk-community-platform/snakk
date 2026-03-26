namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionDebate")]
public class DiscussionDebateDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public bool AllowNeutral { get; set; }

    public virtual ICollection<DiscussionDebatePositionDatabaseEntity> Positions { get; set; } = [];
}
