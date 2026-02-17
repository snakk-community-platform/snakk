using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities;

[Table("Permissions")]
public class PermissionDatabaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public required string PublicId { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Category { get; set; }

    public string? Description { get; set; }

    public bool IsSystemPermission { get; set; } = true;

    [Required]
    public required DateTime CreatedAt { get; set; }

    // Navigation properties
    public ICollection<RolePermissionDatabaseEntity> RolePermissions { get; set; } = new List<RolePermissionDatabaseEntity>();
}
