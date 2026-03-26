namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionMedia")] // Keep existing table name to avoid migration
public class DiscussionGalleryDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public required string Url { get; set; }
    public required string MediaType { get; set; } // e.g. "image/jpeg", "image/png", "image/gif", "image/webp"
    public string? EmbedUrl { get; set; }
    public string? Title { get; set; }
    public string? ThumbnailUrl { get; set; }
}
