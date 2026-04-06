namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ConsentType")]
public class ConsentTypeDatabaseEntity
{
    public int Id { get; set; }

    [MaxLength(50)]
    public required string Slug { get; set; } // e.g. "minimum-age", "terms-of-service", "privacy-policy"

    [MaxLength(100)]
    public required string Name { get; set; } // Display name

    [MaxLength(500)]
    public string? ShortLabel { get; set; } // Checkbox label, e.g. "I agree to the {link}Terms of Service{/link}"

    [MaxLength(500)]
    public string? LinkUrl { get; set; } // URL to full text page, e.g. "/legal/terms"

    public bool IsRequired { get; set; } // Blocks platform use until accepted
    public bool IsActive { get; set; } = true; // Can retire old consent types
    public int DisplayOrder { get; set; } // Controls rendering order

    public required DateTime CreatedAt { get; set; }

    // Navigation
    public virtual ICollection<ConsentTypeVersionDatabaseEntity> Versions { get; set; } = [];
}
