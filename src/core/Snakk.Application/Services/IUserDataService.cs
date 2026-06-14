namespace Snakk.Application.Services;

/// <summary>
/// Thin read-only data-access seam for user-specific queries that would otherwise
/// require a direct SnakkDbContext reference in Snakk.Api.
/// </summary>
public interface IUserDataService
{
    /// <summary>
    /// Returns the Discord connection info for the user with the given public ID,
    /// or <c>null</c> if no Discord account is linked.
    /// </summary>
    Task<UserDiscordInfoDto?> GetDiscordInfoAsync(string publicId, CancellationToken ct = default);
}

public record UserDiscordInfoDto(string DiscordUserId, string? DiscordUsername);
