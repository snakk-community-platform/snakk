namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("PollOption")]
public class PollOptionDatabaseEntity
{
    public int Id { get; set; }

    public int PollId { get; set; }
    public virtual DiscussionPollDatabaseEntity Poll { get; set; } = null!;

    public required string Text { get; set; }
    public int DisplayOrder { get; set; }
    public int VoteCount { get; set; } // Denormalized for performance

    public virtual ICollection<PollVoteDatabaseEntity> Votes { get; set; } = [];
}
