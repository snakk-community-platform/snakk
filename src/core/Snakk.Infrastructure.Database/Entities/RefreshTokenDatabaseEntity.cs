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
    [MaxLength(100)]
    public required string TokenValue { get; set; }

    [Required]
    [MaxLength(50)]
    public required string UserId { get; set; }

    public required DateTime ExpiresAt { get; set; }
    public required DateTime CreatedAt { get; set; }
    public string? RevokedAt { get; set; }

    // Navigation
    public virtual UserDatabaseEntity User { get; set; } = null!;
}
