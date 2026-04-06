namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeImage")]
public class DiscussionTypeImageDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    [MaxLength(20)]
    public string Layout { get; set; } = "grid"; // grid, masonry, justified, carousel, hero

    public virtual ICollection<DiscussionTypeImageAttachmentDatabaseEntity> Images { get; set; } = [];
}
