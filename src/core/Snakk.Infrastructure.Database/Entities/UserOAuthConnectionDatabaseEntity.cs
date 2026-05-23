namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("UserOAuthConnection")]
public class UserOAuthConnectionDatabaseEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [MaxLength(50)]
    public required string Provider { get; set; }

    [MaxLength(200)]
    public required string ProviderUserId { get; set; }

    public DateTime ConnectedAt { get; set; }
    public bool Require2FA { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }

    public virtual UserDatabaseEntity User { get; set; } = null!;
}
