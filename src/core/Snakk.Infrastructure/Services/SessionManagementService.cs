using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Session;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class SessionManagementService(
    SnakkDbContext context,
    IJwtTokenService jwtTokenService,
    ILogger<SessionManagementService> logger) : ISessionManagementService
{
    public async Task<SessionListResponse> GetActiveSessionsAsync(
        string userId,
        string? currentRefreshToken = null,
        CancellationToken ct = default)
    {
        // Get user's database ID
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            logger.LogWarning("User not found for session query: {UserId}", userId);
            return new SessionListResponse
            {
                Sessions = [],
                ActiveCount = 0
            };
        }

        var now = DateTime.UtcNow;

        var raw = await context.RefreshTokens
            .Where(t =>
                t.UserId == user.Id
                && t.RevokedAt == null
                && t.ExpiresAt > now)
            .OrderByDescending(t => t.LastUsedAt ?? t.CreatedAt)
            .Select(t => new
            {
                t.PublicId,
                t.IpAddress,
                t.UserAgent,
                t.DeviceName,
                t.CreatedAt,
                t.ExpiresAt,
                t.TokenValue
            })
            .ToListAsync(ct);

        var sessions = raw.Select(t => new SessionDto
        {
            Id = t.PublicId,
            IpAddress = t.IpAddress ?? "",
            UserAgent = t.UserAgent ?? "",
            DeviceHint = !string.IsNullOrEmpty(t.DeviceName) ? t.DeviceName : ParseDeviceHint(t.UserAgent),
            CreatedAt = t.CreatedAt,
            ExpiresAt = t.ExpiresAt,
            IsCurrent = t.TokenValue == currentRefreshToken
        }).ToList();

        return new SessionListResponse
        {
            Sessions = sessions,
            ActiveCount = sessions.Count
        };
    }

    public async Task<bool> RevokeSessionAsync(string sessionId, string userId, CancellationToken ct = default)
    {
        // Get user's database ID
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            logger.LogWarning("User not found for session revocation: {UserId}", userId);
            return false;
        }

        // Find session and verify ownership
        var session = await context.RefreshTokens
            .AsTracking()
            .Where(t =>
                t.PublicId == sessionId
                && t.UserId == user.Id
                && t.RevokedAt == null)
            .FirstOrDefaultAsync(ct);

        if (session is null)
        {
            logger.LogWarning("Session not found or not owned by user: {SessionId}, {UserId}", sessionId, userId);
            return false;
        }

        session.RevokedAt = DateTime.UtcNow;
        session.RevocationReason = "User requested revocation";
        await context.SaveChangesAsync(ct);

        jwtTokenService.RevokeSession(sessionId);

        logger.LogInformation("Session revoked: {SessionId} for user {UserId}", sessionId, userId);

        return true;
    }

    public async Task<int> RevokeAllExceptAsync(string userId, string excludeSessionId, CancellationToken ct = default)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            logger.LogWarning("User not found for bulk session revocation: {UserId}", userId);
            return 0;
        }

        var now = DateTime.UtcNow;
        // AsTracking() so the RevokedAt / RevocationReason mutations below actually
        // persist. The companion RevokeSessionAsync method already uses it (line 90);
        // this bulk path was missing the call, so "Sign out of all other sessions"
        // returned a count but did not actually revoke any sessions (CR-27).
        var tokensToRevoke = await context.RefreshTokens
            .AsTracking()
            .Where(t =>
                t.UserId == user.Id
                && t.PublicId != excludeSessionId
                && t.RevokedAt == null
                && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in tokensToRevoke)
        {
            token.RevokedAt = now;
            token.RevocationReason = "User revoked all other sessions";
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation("Revoked {Count} sessions for user {UserId} (kept {ExcludedId})",
            tokensToRevoke.Count, userId, excludeSessionId);

        return tokensToRevoke.Count;
    }

    public async Task LogLoginAsync(string userPublicId, string? ipAddress, string? userAgent, bool success, CancellationToken ct = default)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null) return;

        var entry = new LoginHistoryDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            Success = success,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceHint = ParseDeviceHint(userAgent)
        };

        context.LoginHistory.Add(entry);
        await context.SaveChangesAsync(ct);
    }

    public async Task<LoginHistoryListResponse> GetLoginHistoryAsync(string userId, int limit = 20, CancellationToken ct = default)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return new LoginHistoryListResponse { Entries = [] };

        var entries = await context.LoginHistory
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(limit)
            .Select(h => new LoginHistoryDto
            {
                Id = h.PublicId,
                CreatedAt = h.CreatedAt,
                Success = h.Success,
                IpAddress = h.IpAddress,
                DeviceHint = h.DeviceHint
            })
            .ToListAsync(ct);

        return new LoginHistoryListResponse { Entries = entries };
    }

    private static string? ParseDeviceHint(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return null;

        if (userAgent.Contains("iPhone")) return "iPhone";
        if (userAgent.Contains("iPad")) return "iPad";
        if (userAgent.Contains("Android") && userAgent.Contains("Mobile")) return "Android phone";
        if (userAgent.Contains("Android")) return "Android tablet";

        var os = userAgent.Contains("Windows") ? "Windows"
               : userAgent.Contains("Mac OS X") ? "macOS"
               : userAgent.Contains("Linux") ? "Linux"
               : null;

        var browser = userAgent.Contains("Edg/") ? "Edge"
                    : userAgent.Contains("Chrome/") ? "Chrome"
                    : userAgent.Contains("Firefox/") ? "Firefox"
                    : userAgent.Contains("Safari/") ? "Safari"
                    : null;

        if (browser is not null && os is not null) return $"{browser} on {os}";
        if (browser is not null) return browser;
        if (os is not null) return os;
        return null;
    }
}
