namespace Snakk.Application.DTOs.Security;

public class RolePermissionDto
{
    public required int RoleId { get; set; }
    public required string RoleName { get; set; }
    public required int PermissionId { get; set; }
    public required string PermissionName { get; set; }
    public required DateTime GrantedAt { get; set; }
    public int? GrantedById { get; set; }
    public string? GrantedByEmail { get; set; }
}
