namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("UserSocialLink")]
public class UserSocialLinkDatabaseEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserPublicId { get; set; }

    [MaxLength(50)]
    public required string Platform { get; set; }

    [MaxLength(500)]
    public required string Username { get; set; }

    public virtual UserDatabaseEntity User { get; set; } = null!;
}
