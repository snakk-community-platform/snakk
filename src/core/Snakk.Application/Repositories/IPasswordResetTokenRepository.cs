namespace Snakk.Application.Repositories;

public interface IPasswordResetTokenRepository
{
    Task CreateAsync(
        int userId,
        string tokenHash,
        DateTime expiresAt,
        string? createdFromIp,
        string? createdUserAgent);

    Task<PasswordResetTokenLookupDto?> GetByTokenHashAsync(string tokenHash);

    Task MarkUsedAsync(int tokenId, string? usedFromIp, string? usedUserAgent);

    /// <summary>
    /// Invalidates all outstanding (unused, unexpired) tokens for a user.
    /// Called after a successful password reset or change to prevent replay.
    /// </summary>
    Task InvalidateAllForUserAsync(int userId);
}

public record PasswordResetTokenLookupDto(
    int Id,
    int UserId,
    string UserPublicId,
    DateTime ExpiresAt,
    DateTime? UsedAt);
