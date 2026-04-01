namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("IamaOfficialAnswer")]
public class IamaOfficialAnswerDatabaseEntity
{
    public int Id { get; set; }

    public int IamaId { get; set; }
    public virtual DiscussionIamaDatabaseEntity Iama { get; set; } = null!;

    public int QuestionPostId { get; set; }
    public virtual PostDatabaseEntity QuestionPost { get; set; } = null!;

    public int AnswerPostId { get; set; }
    public virtual PostDatabaseEntity AnswerPost { get; set; } = null!;
}
