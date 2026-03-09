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
    public async Task<UserDatabaseEntity?> GetForUpdateAsync(string publicId) => await _dbSet
        .AsTracking()
        .FirstOrDefaultAsync(u => u.PublicId == publicId);

    public async Task<UserDetailDto?> GetForDisplayAsync(string publicId) => await _dbSet
        .Where(u => u.PublicId == publicId)
        .Select(u => new UserDetailDto(
            u.PublicId,
            u.DisplayName,
            u.Email,
            u.CreatedAt,
            u.LastSeenAt))
        .FirstOrDefaultAsync();

    public async Task<UserDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet.FirstOrDefaultAsync(u => u.PublicId == publicId);

    public async Task<IEnumerable<UserDatabaseEntity>> GetByPublicIdsAsync(IEnumerable<string> publicIds)
    {
        var publicIdsList = publicIds.ToList();

        return await _dbSet
            .Where(u => publicIdsList.Contains(u.PublicId))
            .ToListAsync();
    }

    public async Task<UserDatabaseEntity?> GetByEmailAsync(string email) =>
        await _dbSet.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<UserDatabaseEntity?> GetByOAuthProviderIdAsync(string oauthProviderId) =>
        await _dbSet.FirstOrDefaultAsync(u => u.OAuthProviderId == oauthProviderId);

    public async Task<UserDatabaseEntity?> GetByDisplayNameAsync(string displayName) =>
        _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await _dbSet.FirstOrDefaultAsync(u => EF.Functions.ILike(u.DisplayName, displayName))
            : await _dbSet.FirstOrDefaultAsync(u => EF.Functions.Like(u.DisplayName, displayName));

    public async Task<IEnumerable<UserDatabaseEntity>> SearchByDisplayNameAsync(
        string query,
        int limit) => _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
        ? await _dbSet
            .Where(u => EF.Functions.ILike(u.DisplayName, $"%{EscapeLikePattern(query)}%", "\\"))
            .Take(limit)
            .ToListAsync()
        : await _dbSet
            .Where(u => EF.Functions.Like(u.DisplayName, $"%{EscapeLikePattern(query)}%", "\\"))
            .Take(limit)
            .ToListAsync();
}
