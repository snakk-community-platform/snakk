namespace Snakk.Application.Repositories;

public interface IPasswordResetTokenRepository
{
    Task CreateAsync(
        int userId,
        string tokenHash,
        DateTime expiresAt,
        string? createdFromIp,
        string? createdUserAgent,
        CancellationToken ct = default);

    Task<PasswordResetTokenLookupDto?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task MarkUsedAsync(int tokenId, string? usedFromIp, string? usedUserAgent, CancellationToken ct = default);

    /// <summary>
    /// Invalidates all outstanding (unused, unexpired) tokens for a user.
    /// Called after a successful password reset or change to prevent replay.
    /// </summary>
    Task InvalidateAllForUserAsync(int userId, CancellationToken ct = default);
}

public record PasswordResetTokenLookupDto(
    int Id,
    int UserId,
    string UserPublicId,
    DateTime ExpiresAt,
    DateTime? UsedAt);
