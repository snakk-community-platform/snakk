namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("BackupCode")]
public class TwoFactorBackupCodeDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    // The backup code (hashed for security)
    public required string CodeHash { get; set; }

    // Usage tracking
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
    public string? UsedIp { get; set; }

    public required DateTime CreatedAt { get; set; }
}
