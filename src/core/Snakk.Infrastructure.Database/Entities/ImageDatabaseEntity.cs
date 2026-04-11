namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Image")]
public class ImageDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public required string Sha256Hash { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public required long SizeBytes { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public required string StoragePath { get; set; }
    public required DateTime CreatedAt { get; set; }

    public int UploadedByUserId { get; set; }
    public virtual UserDatabaseEntity UploadedByUser { get; set; } = null!;

    public string? ThumbnailPath { get; set; }
    public int? ThumbnailWidth { get; set; }
    public int? ThumbnailHeight { get; set; }

    public string? MediumThumbnailPath { get; set; }
    public int? MediumThumbnailWidth { get; set; }
    public int? MediumThumbnailHeight { get; set; }

    public string? BlurDataUri { get; set; }

    // Lifecycle tracking
    public bool IsDraft { get; set; } = true;
    public DateTime? PublishedAt { get; set; }
    public bool IsReadyForDeletion { get; set; }
    public bool IsDeleted { get; set; }
}
