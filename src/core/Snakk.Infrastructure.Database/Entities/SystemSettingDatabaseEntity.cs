namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("SystemSettings")]
public class SystemSettingDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Setting metadata
    public required string Category { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; } // JSONB stored as string
    public required string ValueType { get; set; } // 'String', 'Integer', 'Boolean', 'JSON'
    public bool IsEncrypted { get; set; } = false;
    public string? Description { get; set; }

    // Audit fields
    public int? UpdatedById { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UpdatedById))]
    public UserDatabaseEntity? UpdatedBy { get; set; }
}
