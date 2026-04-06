namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypePollVote")]
public class DiscussionTypePollVoteDatabaseEntity
{
    public int Id { get; set; }

    public int OptionId { get; set; }
    public virtual DiscussionTypePollOptionDatabaseEntity Option { get; set; } = null!;

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public DateTime VotedAt { get; set; }
}
