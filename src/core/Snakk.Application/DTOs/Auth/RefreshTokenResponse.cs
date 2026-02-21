namespace Snakk.Application.DTOs.Auth;

/// <summary>
/// Response returned from successful token refresh.
/// </summary>
public class RefreshTokenResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
}
