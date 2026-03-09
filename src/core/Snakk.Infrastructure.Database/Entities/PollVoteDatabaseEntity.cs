namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("PollVote")]
public class PollVoteDatabaseEntity
{
    public int Id { get; set; }

    public int OptionId { get; set; }
    public virtual PollOptionDatabaseEntity Option { get; set; } = null!;

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public DateTime VotedAt { get; set; }
}
