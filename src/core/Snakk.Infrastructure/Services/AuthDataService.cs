namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

public class AuthDataService(SnakkDbContext context) : IAuthDataService
{
    // ── Passkey login user projection ─────────────────────────────────────────

    public async Task<PasskeyLoginUserData?> GetPasskeyLoginUserDataAsync(int internalUserId, CancellationToken ct = default)
    {
        return await context.Users
            .Where(u => u.Id == internalUserId)
            .Select(u => new PasskeyLoginUserData(
                u.PublicId,
                u.DisplayName,
                u.Email,
                u.EmailVerified,
                u.AvatarFileName,
                u.AuthVersion,
                u.TwoFactorEnabled,
                u.Slug))
            .FirstOrDefaultAsync(ct);
    }

    // ── Role helpers ──────────────────────────────────────────────────────────

    public async Task<List<string>> GetUserRolesAsync(string publicId, CancellationToken ct = default) =>
        await context.UserRoles
            .Where(r => r.User.PublicId == publicId && r.RevokedAt == null)
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToListAsync(ct);

    // ── 2FA flag ──────────────────────────────────────────────────────────────

    public async Task<bool> GetTwoFactorEnabledAsync(string publicId, CancellationToken ct = default) =>
        await context.Users
            .Where(u => u.PublicId == publicId)
            .Select(u => u.TwoFactorEnabled)
            .FirstOrDefaultAsync(ct);

    // ── Refresh token session info ────────────────────────────────────────────

    public async Task<string?> SetRefreshTokenSessionInfoAsync(
        string tokenHash, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        // Read the session PublicId first (needed for the JWT sid claim)
        var sessionPublicId = await context.RefreshTokens
            .Where(t => t.TokenValue == tokenHash)
            .Select(t => t.PublicId)
            .FirstOrDefaultAsync(ct);

        // Then update IP + UA atomically
        await context.RefreshTokens
            .Where(t => t.TokenValue == tokenHash)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IpAddress, ipAddress)
                .SetProperty(t => t.UserAgent, userAgent),
                ct);

        return sessionPublicId;
    }

    public async Task<string?> GetRefreshTokenSessionIdAsync(string tokenHash, CancellationToken ct = default) =>
        await context.RefreshTokens
            .Where(t => t.TokenValue == tokenHash)
            .Select(t => t.PublicId)
            .FirstOrDefaultAsync(ct);

    // ── OAuth ─────────────────────────────────────────────────────────────────

    public async Task<bool> OAuthConnectionExistsAsync(string provider, string providerUserId, CancellationToken ct = default) =>
        await context.UserOAuthConnections
            .AnyAsync(c => c.Provider == provider && c.ProviderUserId == providerUserId, ct);

    public async Task<bool> GetOAuthConnectionRequire2FAAsync(
        string userPublicId, string provider, CancellationToken ct = default) =>
        await context.UserOAuthConnections
            .Where(c => c.User.PublicId == userPublicId && c.Provider == provider)
            .Select(c => c.Require2FA)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> VerifyOAuthConnectionOwnershipAsync(
        string userPublicId, string provider, string providerUserId, CancellationToken ct = default) =>
        await context.UserOAuthConnections
            .AnyAsync(c =>
                c.User.PublicId == userPublicId &&
                c.Provider == provider &&
                c.ProviderUserId == providerUserId,
                ct);

    // ── Discord ───────────────────────────────────────────────────────────────

    public async Task<DiscordStatusData?> GetDiscordStatusAsync(
        string userPublicId, CancellationToken ct = default)
    {
        var row = await context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => new { u.DiscordUserId, u.DiscordUsername, u.DiscordAvatarHash })
            .FirstOrDefaultAsync(ct);

        if (row?.DiscordUserId is null) return null;

        return new DiscordStatusData(row.DiscordUserId, row.DiscordUsername, row.DiscordAvatarHash);
    }

    public async Task<(string Token, DateTime ExpiresAt)> SetDiscordLinkTokenAsync(
        string userPublicId, CancellationToken ct = default)
    {
        // AsTracking() so the assignments persist via EF change tracking.
        var user = await context.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userPublicId, ct)
            ?? throw new InvalidOperationException($"User {userPublicId} not found");

        var token = Guid.NewGuid().ToString("N");
        var expiry = DateTime.UtcNow.AddMinutes(15);
        user.DiscordLinkToken = token;
        user.DiscordLinkTokenExpiry = expiry;
        await context.SaveChangesAsync(ct);

        return (token, expiry);
    }

    public async Task<(string ResultCode, string? UserPublicId)> CompleteDiscordLinkAsync(
        string linkToken,
        string discordUserId,
        string discordUsername,
        string? discordAvatarHash,
        CancellationToken ct = default)
    {
        // AsTracking() so mutations below actually persist (CR-25).
        var user = await context.Users
            .AsTracking()
            .FirstOrDefaultAsync(u =>
                u.DiscordLinkToken == linkToken &&
                u.DiscordLinkTokenExpiry != null &&
                u.DiscordLinkTokenExpiry > DateTime.UtcNow,
                ct);

        if (user is null)
            return ("TOKEN_INVALID", null);

        var alreadyLinked = await context.Users
            .AnyAsync(u => u.DiscordUserId == discordUserId && u.PublicId != user.PublicId, ct);
        if (alreadyLinked)
            return ("ALREADY_LINKED", null);

        user.DiscordUserId = discordUserId;
        user.DiscordUsername = discordUsername;
        user.DiscordAvatarHash = discordAvatarHash;
        user.DiscordLinkToken = null;
        user.DiscordLinkTokenExpiry = null;
        await context.SaveChangesAsync(ct);

        return ("OK", user.PublicId);
    }

    public async Task UnlinkDiscordAsync(string userPublicId, CancellationToken ct = default)
    {
        // AsTracking() so field nulling below persists (CR-25).
        var user = await context.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userPublicId, ct)
            ?? throw new InvalidOperationException($"User {userPublicId} not found");

        user.DiscordUserId = null;
        user.DiscordUsername = null;
        user.DiscordAvatarHash = null;
        user.DiscordLinkToken = null;
        user.DiscordLinkTokenExpiry = null;
        await context.SaveChangesAsync(ct);
    }
}
