using Snakk.Application.DTOs.Session;

namespace Snakk.Application.Services;

/// <summary>
/// Service for managing user sessions and refresh tokens
/// </summary>
public interface ISessionManagementService
{
    Task<SessionListResponse> GetActiveSessionsAsync(string userId, string? currentRefreshTokenHash = null, CancellationToken ct = default);
    Task<bool> RevokeSessionAsync(string sessionId, string userId, CancellationToken ct = default);
    Task<int> RevokeAllExceptAsync(string userId, string excludeSessionId, CancellationToken ct = default);
    Task LogLoginAsync(string userPublicId, string? ipAddress, string? userAgent, bool success, CancellationToken ct = default);
    Task<LoginHistoryListResponse> GetLoginHistoryAsync(string userId, int limit = 20, CancellationToken ct = default);
}
