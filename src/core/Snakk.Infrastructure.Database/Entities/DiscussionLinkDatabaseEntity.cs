namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionLink")]
public class DiscussionLinkDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public required string Url { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Domain { get; set; }
}
