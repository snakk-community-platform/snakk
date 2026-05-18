using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using System.Security.Cryptography;

namespace Snakk.Infrastructure.Database.Repositories;

public class RefreshTokenRepository(SnakkDbContext context) : IRefreshTokenRepository
{
    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

    public async Task<RefreshToken?> GetByValueAsync(string tokenValue, CancellationToken ct = default)
    {
        var projection = await _context.RefreshTokens
            .Where(t => t.TokenValue == HashToken(tokenValue))
            .Select(t => new RefreshTokenProjection(
                t.TokenValue,
                t.User.PublicId,
                t.ExpiresAt,
                t.CreatedAt,
                t.RevokedAt))
            .FirstOrDefaultAsync(ct);

        return projection?.ToDomain();
    }

    public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var projections = await _context.RefreshTokens
            .Where(t => t.User.PublicId == userId.Value)
            .Select(t => new RefreshTokenProjection(
                t.TokenValue,
                t.User.PublicId,
                t.ExpiresAt,
                t.CreatedAt,
                t.RevokedAt))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        // Look up the user's internal ID
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == token.UserId.Value, ct)
            ?? throw new InvalidOperationException($"User {token.UserId} not found");

        var entity = new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = HashToken(token.Value),
            UserId = user.Id,
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt,
            RevokedAt = string.IsNullOrEmpty(token.RevokedAt) ? null : DateTime.Parse(token.RevokedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
        };

        await _context.RefreshTokens.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RefreshToken token, CancellationToken ct = default)
    {
        var entity = await _context.RefreshTokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.TokenValue == token.Value, ct);

        if (entity is not null)
        {
            entity.RevokedAt = string.IsNullOrEmpty(token.RevokedAt) ? null : DateTime.Parse(token.RevokedAt, null, System.Globalization.DateTimeStyles.RoundtripKind);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAllForUserAsync(UserId userId, CancellationToken ct = default)
    {
        var tokens = await _context.RefreshTokens
            .AsTracking()
            .Where(t =>
                t.User.PublicId == userId.Value
                && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteExpiredTokensAsync(CancellationToken ct = default)
    {
        await _context.RefreshTokens
            .Where(t => t.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);
    }

    private readonly SnakkDbContext _context = context;

    private record RefreshTokenProjection(
        string TokenValue,
        string UserPublicId,
        DateTime ExpiresAt,
        DateTime CreatedAt,
        DateTime? RevokedAt)
    {
        public RefreshToken ToDomain() => RefreshToken.Rehydrate(
            TokenValue,
            UserId.From(UserPublicId),
            ExpiresAt,
            CreatedAt,
            RevokedAt?.ToString("O"));
    }
}
