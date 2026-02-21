namespace Snakk.Application.DTOs.Auth;

/// <summary>
/// Response returned from session-based token refresh.
/// </summary>
public class SessionRefreshResponse
{
    public required string AccessToken { get; set; }
    public string Message { get; set; } = "Token refreshed successfully";
}
