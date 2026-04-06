using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities;

[Table("PasswordResetToken")]
public class PasswordResetTokenDatabaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public required string PublicId { get; set; }

    /// <summary>SHA-256 hash of the raw token. Raw token is only delivered via email link.</summary>
    [Required]
    [MaxLength(128)]
    public required string TokenHash { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public required DateTime CreatedAt { get; set; }
    public required DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    [MaxLength(64)]
    public string? CreatedFromIp { get; set; }

    [MaxLength(512)]
    public string? CreatedUserAgent { get; set; }

    [MaxLength(64)]
    public string? UsedFromIp { get; set; }

    [MaxLength(512)]
    public string? UsedUserAgent { get; set; }

    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsUsed => UsedAt.HasValue;

    [NotMapped]
    public bool IsValid => !IsExpired && !IsUsed;
}
