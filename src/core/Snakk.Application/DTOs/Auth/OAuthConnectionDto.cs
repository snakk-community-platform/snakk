namespace Snakk.Application.DTOs.Auth;

public record OAuthConnectionDto(
    string Provider,
    DateTime ConnectedAt,
    DateTime? LastLoginAt,
    bool Require2FA);
