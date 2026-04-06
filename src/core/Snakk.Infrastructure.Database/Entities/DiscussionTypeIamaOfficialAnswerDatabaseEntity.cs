namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeIamaOfficialAnswer")]
public class DiscussionTypeIamaOfficialAnswerDatabaseEntity
{
    public int Id { get; set; }

    public int IamaId { get; set; }
    public virtual DiscussionTypeIamaDatabaseEntity Iama { get; set; } = null!;

    public int QuestionPostId { get; set; }
    public virtual PostDatabaseEntity QuestionPost { get; set; } = null!;

    public int AnswerPostId { get; set; }
    public virtual PostDatabaseEntity AnswerPost { get; set; } = null!;
}
