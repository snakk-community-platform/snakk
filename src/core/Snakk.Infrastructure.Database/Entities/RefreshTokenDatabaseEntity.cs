using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities;

[Table("RefreshToken")]
public class RefreshTokenDatabaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public required string PublicId { get; set; }

    [Required]
    [MaxLength(100)]
    public required string TokenValue { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public required DateTime ExpiresAt { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Revocation support
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }

    // Token rotation support (one-time use tokens)
    public int? ReplacedByTokenId { get; set; }
    public virtual RefreshTokenDatabaseEntity? ReplacedByToken { get; set; }

    // Device & session tracking
    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime? LastUsedAt { get; set; }

    // Helper properties
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsRevoked => RevokedAt.HasValue;

    [NotMapped]
    public bool IsActive => !IsExpired && !IsRevoked && ReplacedByTokenId == null;
}
