namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypePoll")]
public class DiscussionTypePollDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public bool AllowMultipleChoices { get; set; }
    public bool AllowChangeVote { get; set; }
    public bool VotesVisible { get; set; } = true;
    public DateTime? ClosesAt { get; set; }

    public bool IsSegmented { get; set; }
    public string? SegmentLabel { get; set; }
    public string? SegmentOptionA { get; set; }
    public string? SegmentOptionB { get; set; }

    public virtual ICollection<DiscussionTypePollOptionDatabaseEntity> Options { get; set; } = [];
}
