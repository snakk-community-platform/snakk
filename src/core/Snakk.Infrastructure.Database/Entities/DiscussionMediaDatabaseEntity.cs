namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionMedia")]
public class DiscussionMediaDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public required string Url { get; set; }
    public required string MediaType { get; set; } // e.g. "youtube", "vimeo", "soundcloud", "twitch", "twitter"
    public string? EmbedUrl { get; set; }
    public string? Title { get; set; }
    public string? ThumbnailUrl { get; set; }
}
