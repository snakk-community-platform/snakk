namespace Snakk.Application.Services;

/// <summary>
/// Service for authorization-related queries
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Check if a user has two-factor authentication enabled
    /// </summary>
    Task<bool> UserHas2FAEnabledAsync(string userId, CancellationToken ct = default);
}
