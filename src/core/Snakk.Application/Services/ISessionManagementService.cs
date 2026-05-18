using Snakk.Application.DTOs.Session;

namespace Snakk.Application.Services;

/// <summary>
/// Service for managing user sessions and refresh tokens
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Get all active sessions for a user
    /// </summary>
    Task<SessionListResponse> GetActiveSessionsAsync(string userId, string? currentRefreshToken = null, CancellationToken ct = default);

    /// <summary>
    /// Revoke a specific session
    /// </summary>
    Task<bool> RevokeSessionAsync(string sessionId, string userId, CancellationToken ct = default);
}
