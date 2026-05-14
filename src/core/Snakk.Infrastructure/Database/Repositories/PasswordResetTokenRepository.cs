namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class PasswordResetTokenRepository(SnakkDbContext context) : IPasswordResetTokenRepository
{
    public async Task CreateAsync(
        int userId,
        string tokenHash,
        DateTime expiresAt,
        string? createdFromIp,
        string? createdUserAgent)
    {
        var entity = new PasswordResetTokenDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            TokenHash = tokenHash,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            CreatedFromIp = createdFromIp,
            CreatedUserAgent = createdUserAgent
        };

        await context.PasswordResetTokens.AddAsync(entity);
        await context.SaveChangesAsync();
    }

    public async Task<PasswordResetTokenLookupDto?> GetByTokenHashAsync(string tokenHash)
        => await context.PasswordResetTokens
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => new PasswordResetTokenLookupDto(
                t.Id,
                t.UserId,
                t.User.PublicId,
                t.ExpiresAt,
                t.UsedAt))
            .FirstOrDefaultAsync();

    public async Task MarkUsedAsync(int tokenId, string? usedFromIp, string? usedUserAgent)
    {
        var entity = await context.PasswordResetTokens.AsTracking().FirstOrDefaultAsync(t => t.Id == tokenId);
        if (entity is null) return;

        entity.UsedAt = DateTime.UtcNow;
        entity.UsedFromIp = usedFromIp;
        entity.UsedUserAgent = usedUserAgent;
        await context.SaveChangesAsync();
    }

    public async Task InvalidateAllForUserAsync(int userId)
    {
        var now = DateTime.UtcNow;
        await context.PasswordResetTokens
            .Where(t =>
                t.UserId == userId
                && t.UsedAt == null
                && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now));
    }
}
