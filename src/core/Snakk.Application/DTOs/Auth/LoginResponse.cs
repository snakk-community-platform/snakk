namespace Snakk.Application.DTOs.Auth;

/// <summary>
/// Response returned from successful login.
/// </summary>
public class LoginResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required UserInfo User { get; set; }
}

/// <summary>
/// User information included in login response.
/// </summary>
public class UserInfo
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public bool EmailVerified { get; set; }
    public required List<string> Roles { get; set; }
}
