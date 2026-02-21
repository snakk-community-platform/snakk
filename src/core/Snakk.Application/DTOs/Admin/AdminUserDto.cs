namespace Snakk.Application.DTOs.Admin;

public class AdminUserDto
{
    public required string PublicId { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public required DateTime CreatedAt { get; set; }
}
