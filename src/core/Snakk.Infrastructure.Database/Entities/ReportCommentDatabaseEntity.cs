namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("ReportComment")]
public class ReportCommentDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int ReportId { get; set; }
    public virtual ReportDatabaseEntity Report { get; set; } = null!;

    public int AuthorUserId { get; set; }
    public virtual UserDatabaseEntity AuthorUser { get; set; } = null!;

    public required string Content { get; set; }

    public required DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
