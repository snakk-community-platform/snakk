namespace Snakk.Infrastructure.Database.Entities;

public class GalleryImageDatabaseEntity
{
    public int Id { get; set; }

    public int GalleryId { get; set; }
    public virtual DiscussionGalleryDatabaseEntity Gallery { get; set; } = null!;

    public int MediaId { get; set; }
    public virtual MediaDatabaseEntity Media { get; set; } = null!;

    public int DisplayOrder { get; set; }
}
