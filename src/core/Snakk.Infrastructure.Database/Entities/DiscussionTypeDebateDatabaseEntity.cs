namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeDebate")]
public class DiscussionTypeDebateDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public bool AllowNeutral { get; set; }

    public virtual ICollection<DiscussionTypeDebatePositionDatabaseEntity> Positions { get; set; } = [];
}
