using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities;

[Table("TemporaryRoleElevations")]
public class TemporaryRoleElevationDatabaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public required string PublicId { get; set; }

    [Required]
    public required int UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public required string RoleType { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Scope { get; set; }

    [Required]
    public required int ScopeId { get; set; }

    [Required]
    public required DateTime ExpiresAt { get; set; }

    public string? Reason { get; set; }

    [Required]
    public required int GrantedById { get; set; }

    public DateTime? RevokedAt { get; set; }

    public int? RevokedById { get; set; }

    public string? RevokedReason { get; set; }

    [Required]
    public required DateTime CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public UserDatabaseEntity? User { get; set; }

    [ForeignKey(nameof(GrantedById))]
    public UserDatabaseEntity? GrantedBy { get; set; }

    [ForeignKey(nameof(RevokedById))]
    public UserDatabaseEntity? RevokedBy { get; set; }
}
