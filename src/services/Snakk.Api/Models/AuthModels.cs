namespace Snakk.Api.Models;

public record RegisterRequest(
    string Email,
    string Password,
    string DisplayName);

public record LoginRequest(
    string Email,
    string Password);

public record UpdateProfileRequest(
    string DisplayName);

public record UpdatePreferencesRequest(
    bool? AutoFollowOnReply,
    string? Timezone = null);

public record RefreshTokenRequest(string RefreshToken);
