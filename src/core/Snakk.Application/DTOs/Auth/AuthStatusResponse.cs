namespace Snakk.Application.DTOs.Auth;

public class AuthStatusResponse
{
    public required bool IsAuthenticated { get; set; }
    public string? PublicId { get; set; }
    public string? DisplayName { get; set; }
    public bool? EmailVerified { get; set; }
    public string? Role { get; set; }
    public string? AvatarUrl { get; set; }
}
