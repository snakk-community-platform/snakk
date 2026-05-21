namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("PasskeyCredential")]
public class PasskeyCredentialDatabaseEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public required byte[] CredentialId { get; set; }
    public required byte[] PublicKey { get; set; }
    public uint SignCount { get; set; }

    // JSON array of transport strings, e.g. ["internal","hybrid"]
    public string? Transports { get; set; }
    public Guid AaGuid { get; set; }

    // User-given label, e.g. "MacBook Touch ID"
    public string? FriendlyName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public virtual UserDatabaseEntity User { get; set; } = null!;
}
