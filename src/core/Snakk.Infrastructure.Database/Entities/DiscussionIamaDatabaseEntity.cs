namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionIama")]
public class DiscussionIamaDatabaseEntity
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

    public virtual ICollection<IamaOfficialAnswerDatabaseEntity> OfficialAnswers { get; set; } = [];
    public virtual ICollection<IamaBestQuestionDatabaseEntity> BestQuestions { get; set; } = [];
}
