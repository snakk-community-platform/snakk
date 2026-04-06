namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("ConsentTypeVersion")]
public class ConsentTypeVersionDatabaseEntity
{
    public int Id { get; set; }

    public int ConsentTypeId { get; set; }
    public virtual ConsentTypeDatabaseEntity ConsentType { get; set; } = null!;

    public int VersionNumber { get; set; } // Incrementing per type

    [Column(TypeName = "text")]
    public required string Text { get; set; } // Full legal text (markdown)

    public required DateTime CreatedAt { get; set; }
}
