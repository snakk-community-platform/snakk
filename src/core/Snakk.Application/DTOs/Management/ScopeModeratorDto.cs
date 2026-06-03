namespace Snakk.Application.DTOs.Management;

public class ScopeModeratorDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime AssignedAt { get; set; }
}
