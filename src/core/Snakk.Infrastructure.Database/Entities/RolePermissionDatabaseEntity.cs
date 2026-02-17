using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities;

[Table("RolePermissions")]
public class RolePermissionDatabaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public required int RoleId { get; set; }

    [Required]
    public required int PermissionId { get; set; }

    [Required]
    public required DateTime GrantedAt { get; set; }

    public int? GrantedById { get; set; }

    // Navigation properties
    [ForeignKey(nameof(RoleId))]
    public UserRoleDatabaseEntity? Role { get; set; }

    [ForeignKey(nameof(PermissionId))]
    public PermissionDatabaseEntity? Permission { get; set; }

    [ForeignKey(nameof(GrantedById))]
    public UserDatabaseEntity? GrantedBy { get; set; }
}
