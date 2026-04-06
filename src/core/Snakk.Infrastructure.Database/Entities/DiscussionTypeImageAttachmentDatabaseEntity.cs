namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeImageAttachment")]
public class DiscussionTypeImageAttachmentDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionTypeImageId { get; set; }
    public virtual DiscussionTypeImageDatabaseEntity DiscussionTypeImage { get; set; } = null!;

    public int ImageId { get; set; }
    public virtual ImageDatabaseEntity Image { get; set; } = null!;

    public int DisplayOrder { get; set; }
}
