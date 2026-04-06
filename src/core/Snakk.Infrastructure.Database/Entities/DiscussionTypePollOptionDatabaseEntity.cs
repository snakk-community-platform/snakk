namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypePollOption")]
public class DiscussionTypePollOptionDatabaseEntity
{
    public int Id { get; set; }

    public int PollId { get; set; }
    public virtual DiscussionTypePollDatabaseEntity Poll { get; set; } = null!;

    public required string Text { get; set; }
    public int DisplayOrder { get; set; }
    public int VoteCount { get; set; } // Denormalized for performance

    public virtual ICollection<DiscussionTypePollVoteDatabaseEntity> Votes { get; set; } = [];
}
