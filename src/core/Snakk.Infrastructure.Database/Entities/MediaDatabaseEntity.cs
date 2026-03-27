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

    // Optimized versions for fast rendering
    public string? ThumbnailPath { get; set; } // 300px longest side, proportional
    public string? BlurDataUri { get; set; } // ~20px base64 data URI for blur-up placeholder

    // Lifecycle tracking
    public bool IsDraft { get; set; } = true;
    public DateTime? PublishedAt { get; set; }
    public bool IsReadyForDeletion { get; set; }
    public bool IsDeleted { get; set; }
}
