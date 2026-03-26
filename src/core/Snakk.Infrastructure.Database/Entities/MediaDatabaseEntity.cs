namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Media")]
public class MediaDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public required string Sha256Hash { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public required long SizeBytes { get; set; }
    public required string StoragePath { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Who uploaded this file
    public int UploadedByUserId { get; set; }
    public virtual UserDatabaseEntity UploadedByUser { get; set; } = null!;

    // Draft tracking — uploads are drafts until linked to a published discussion
    public bool IsDraft { get; set; } = true;
    public DateTime? DraftExpiresAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
