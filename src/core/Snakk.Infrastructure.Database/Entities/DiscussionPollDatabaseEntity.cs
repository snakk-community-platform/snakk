namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionPoll")]
public class DiscussionPollDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public bool AllowMultipleChoices { get; set; }
    public bool AllowChangeVote { get; set; }
    public bool VotesVisible { get; set; } = true;
    public DateTime? ClosesAt { get; set; }

    public virtual ICollection<PollOptionDatabaseEntity> Options { get; set; } = [];
}
