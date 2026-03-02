namespace Snakk.Application.DTOs.Security;

public class RolePermissionDto
{
    public required string RoleName { get; set; }
    public required string PermissionName { get; set; }
    public required DateTime GrantedAt { get; set; }
    public string? GrantedByDisplayName { get; set; }
}
