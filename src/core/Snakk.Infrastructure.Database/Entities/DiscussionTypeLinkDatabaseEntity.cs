namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeLink")]
public class DiscussionTypeLinkDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public required string Url { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Domain { get; set; }
    public string? OEmbedHtml { get; set; }
    public string? ImagePath { get; set; }
    public string? ImageThumbnailPath { get; set; }
    public string? ImageBlurDataUri { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public bool IsInternal { get; set; }
}
