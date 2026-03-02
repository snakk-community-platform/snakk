using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Database.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly SnakkDbContext _context;

    public RefreshTokenRepository(SnakkDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByValueAsync(string tokenValue)
    {
        var projection = await _context.RefreshTokens
            .Where(t => t.TokenValue == tokenValue)
            .Select(t => new RefreshTokenProjection(
                t.TokenValue, t.User.PublicId, t.ExpiresAt, t.CreatedAt, t.RevokedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(UserId userId)
    {
        var projections = await _context.RefreshTokens
            .Where(t => t.User.PublicId == userId.Value)
            .Select(t => new RefreshTokenProjection(
                t.TokenValue, t.User.PublicId, t.ExpiresAt, t.CreatedAt, t.RevokedAt))
            .ToListAsync();
        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(RefreshToken token)
    {
        // Look up the user's internal ID
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == token.UserId.Value);
        if (user == null)
            throw new InvalidOperationException($"User {token.UserId} not found");

        var entity = new RefreshTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenValue = token.Value,
            UserId = user.Id,
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt,
            RevokedAt = string.IsNullOrEmpty(token.RevokedAt) ? null : DateTime.Parse(token.RevokedAt)
        };

        await _context.RefreshTokens.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(RefreshToken token)
    {
        var entity = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenValue == token.Value);

        if (entity != null)
        {
            entity.RevokedAt = string.IsNullOrEmpty(token.RevokedAt) ? null : DateTime.Parse(token.RevokedAt);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllForUserAsync(UserId userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.User.PublicId == userId.Value && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteExpiredTokensAsync()
    {
        var expiredTokens = await _context.RefreshTokens
            .Where(t => t.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(expiredTokens);
        await _context.SaveChangesAsync();
    }

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
            ExpiresAt, CreatedAt,
            RevokedAt?.ToString("O"));
    }
}
