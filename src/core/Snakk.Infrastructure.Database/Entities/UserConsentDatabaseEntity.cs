namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("UserConsent")]
public class UserConsentDatabaseEntity
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public int ConsentTypeVersionId { get; set; }
    public virtual ConsentTypeVersionDatabaseEntity ConsentTypeVersion { get; set; } = null!;

    public required DateTime AcceptedAt { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }
}
