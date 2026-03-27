namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionMedia")] // Keep existing table name to avoid migration
public class DiscussionGalleryDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    [MaxLength(20)]
    public string Layout { get; set; } = "grid"; // grid, masonry, justified, carousel, hero

    public virtual ICollection<GalleryImageDatabaseEntity> Images { get; set; } = [];
}
