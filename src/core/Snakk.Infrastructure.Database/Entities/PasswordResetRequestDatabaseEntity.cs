using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities;

/// <summary>
/// Audit log + rate limit source for password reset requests.
/// Stores SHA-256 hash of email (not raw email) to minimize PII retention.
/// </summary>
[Table("PasswordResetRequest")]
public class PasswordResetRequestDatabaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public required string EmailHash { get; set; }

    [Required]
    [MaxLength(64)]
    public required string IpAddress { get; set; }

    public required DateTime RequestedAt { get; set; }

    public int Outcome { get; set; } // Maps to PasswordResetRequestOutcomeEnum
}
