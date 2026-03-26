namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionQuestion")]
public class DiscussionQuestionDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public int? AcceptedPostId { get; set; }
    public virtual PostDatabaseEntity? AcceptedPost { get; set; }

    public DateTime? SolvedAt { get; set; }
}
