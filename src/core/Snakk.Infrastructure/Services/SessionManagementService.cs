using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Session;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class SessionManagementService : ISessionManagementService
{
    private readonly SnakkDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ILogger<SessionManagementService> _logger;

    public SessionManagementService(
        SnakkDbContext context,
        ITokenService tokenService,
        ILogger<SessionManagementService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<SessionListResponse> GetActiveSessionsAsync(string userId, string? currentRefreshToken = null)
    {
        // Get user's database ID
        var user = await _context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogWarning("User not found for session query: {UserId}", userId);
            return new SessionListResponse
            {
                Sessions = new List<SessionDto>(),
                ActiveCount = 0
            };
        }

        var now = DateTime.UtcNow;
        var sessions = await _context.RefreshTokens
            .Where(t =>
                t.UserId == user.Id &&
                t.RevokedAt == null &&
                t.ExpiresAt > now)
            .OrderByDescending(t => t.LastUsedAt ?? t.CreatedAt)
            .Select(t => new SessionDto
            {
                Id = t.PublicId,
                IpAddress = t.IpAddress,
                UserAgent = t.UserAgent,
                CreatedAt = t.CreatedAt,
                ExpiresAt = t.ExpiresAt,
                IsCurrent = t.TokenValue == currentRefreshToken
            })
            .ToListAsync();

        return new SessionListResponse
        {
            Sessions = sessions,
            ActiveCount = sessions.Count
        };
    }

    public async Task<bool> RevokeSessionAsync(string sessionId, string userId)
    {
        // Get user's database ID
        var user = await _context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogWarning("User not found for session revocation: {UserId}", userId);
            return false;
        }

        // Find session and verify ownership
        var session = await _context.RefreshTokens
            .Where(t => t.PublicId == sessionId && t.UserId == user.Id)
            .Select(t => new { t.TokenValue })
            .FirstOrDefaultAsync();

        if (session == null)
        {
            _logger.LogWarning("Session not found or not owned by user: {SessionId}, {UserId}", sessionId, userId);
            return false;
        }

        // Revoke via token service
        await _tokenService.RevokeRefreshTokenAsync(session.TokenValue, "User requested revocation");

        _logger.LogInformation("Session revoked: {SessionId} for user {UserId}", sessionId, userId);
        return true;
    }
}
