namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("TrustedDevice")]
public class TrustedDeviceDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    // Device identification (hash of User-Agent + IP for privacy)
    public required string DeviceFingerprint { get; set; }
    public required string DeviceName { get; set; } // Friendly name: "Chrome on Windows", "Firefox on Linux", etc.

    // Trust period
    public required DateTime TrustedAt { get; set; }
    public DateTime? ExpiresAt { get; set; } // null = never expires

    // Last usage tracking
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }

    // Revocation
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}
