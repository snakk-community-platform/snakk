namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserDatabaseEntity>(context), IUserRepository
{
    private static string EscapeLikePattern(string input) => input
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
    public async Task<UserDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .AsTracking()
        .FirstOrDefaultAsync(u => u.PublicId == publicId, ct);

    public async Task<UserDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Where(u => u.PublicId == publicId)
        .Select(u => new UserDetailDto(
            u.PublicId,
            u.DisplayName ?? "",
            u.Email,
            u.CreatedAt,
            u.LastSeenAt))
        .FirstOrDefaultAsync(ct);

    public async Task<UserDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(u => u.PublicId == publicId, ct);

    public async Task<IEnumerable<UserDatabaseEntity>> GetByPublicIdsAsync(IEnumerable<string> publicIds, CancellationToken ct = default)
    {
        var publicIdsList = publicIds.ToList();

        return await _dbSet
            .Where(u => publicIdsList.Contains(u.PublicId))
            .ToListAsync(ct);
    }

    public async Task<UserDatabaseEntity?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<UserDatabaseEntity?> GetByOAuthProviderIdAsync(string oauthProviderId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(u => u.OAuthProviderId == oauthProviderId, ct);

    public async Task<UserDatabaseEntity?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default) =>
        _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await _dbSet.FirstOrDefaultAsync(u => EF.Functions.ILike(u.DisplayName!, displayName), ct)
            : await _dbSet.FirstOrDefaultAsync(u => EF.Functions.Like(u.DisplayName!, displayName), ct);

    public async Task<IEnumerable<UserDatabaseEntity>> SearchByDisplayNameAsync(
        string query,
        int limit,
        CancellationToken ct = default) => _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
        ? await _dbSet
            .Where(u => EF.Functions.ILike(u.DisplayName!, $"%{EscapeLikePattern(query)}%", "\\"))
            .Take(limit)
            .ToListAsync(ct)
        : await _dbSet
            .Where(u => EF.Functions.Like(u.DisplayName!, $"%{EscapeLikePattern(query)}%", "\\"))
            .Take(limit)
            .ToListAsync(ct);
}
