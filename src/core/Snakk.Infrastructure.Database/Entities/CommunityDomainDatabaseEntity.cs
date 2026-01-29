namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("CommunityDomain")]
public class CommunityDomainDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Foreign key
    public int CommunityId { get; set; }

    // Domain attributes
    public required string Domain { get; set; }  // e.g., "forum.example.com"
    public bool IsPrimary { get; set; }
    public bool IsVerified { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Navigation
    public virtual CommunityDatabaseEntity Community { get; set; } = null!;
}
