namespace Snakk.Application.DTOs.Security;

public class PermissionDto
{
    public required string PublicId { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }
    public bool IsSystemPermission { get; set; }
    public required DateTime CreatedAt { get; set; }
}
