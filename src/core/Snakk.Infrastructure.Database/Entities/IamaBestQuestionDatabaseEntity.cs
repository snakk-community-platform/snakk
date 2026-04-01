namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("IamaBestQuestion")]
public class IamaBestQuestionDatabaseEntity
{
    public int Id { get; set; }

    public int IamaId { get; set; }
    public virtual DiscussionIamaDatabaseEntity Iama { get; set; } = null!;

    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int DisplayOrder { get; set; }
}
