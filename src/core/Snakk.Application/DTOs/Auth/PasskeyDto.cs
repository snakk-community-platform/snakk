namespace Snakk.Application.DTOs.Auth;

public record PasskeyDto(
    int Id,
    string? FriendlyName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    string? Transports);
