using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

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
        var entity = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenValue == tokenValue);

        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(UserId userId)
    {
        var entities = await _context.RefreshTokens
            .Where(t => t.UserId == userId.Value)
            .ToListAsync();

        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddAsync(RefreshToken token)
    {
        var entity = token.ToPersistence();
        await _context.RefreshTokens.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(RefreshToken token)
    {
        var entity = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenValue == token.Value);

        if (entity != null)
        {
            entity.RevokedAt = token.RevokedAt;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllForUserAsync(UserId userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId.Value && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow.ToString("O");
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
}
