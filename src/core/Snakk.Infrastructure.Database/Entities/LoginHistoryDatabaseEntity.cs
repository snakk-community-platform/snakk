using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities;

[Table("LoginHistory")]
public class LoginHistoryDatabaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(36)]
    public required string PublicId { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public required DateTime CreatedAt { get; set; }
    public bool Success { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(150)]
    public string? DeviceHint { get; set; }
}
