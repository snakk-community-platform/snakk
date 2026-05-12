namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeIama")]
public class DiscussionTypeIamaDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    /// <summary>0 = Announced, 1 = Live, 2 = Closed, 3 = Archived</summary>
    public int Phase { get; set; }

    public bool IsScheduled { get; set; }
    public DateTime? ScheduledStartUtc { get; set; }
    public DateTime? ScheduledEndUtc { get; set; }

    [MaxLength(2000)]
    public string? VerificationNote { get; set; }

    public string? VerificationNoteHtml { get; set; }

    public DateTime? ActualStartedAtUtc { get; set; }
    public DateTime? ActualEndedAtUtc { get; set; }

    public int OfficialAnswerCount { get; set; }
    public int BestQuestionCount { get; set; }

    public virtual ICollection<DiscussionTypeIamaOfficialAnswerDatabaseEntity> OfficialAnswers { get; set; } = [];
    public virtual ICollection<DiscussionTypeIamaBestQuestionDatabaseEntity> BestQuestions { get; set; } = [];
}
