namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserDatabaseEntity>(context), IUserRepository
{
    public async Task<UserDatabaseEntity?> GetForUpdateAsync(string publicId)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.PublicId == publicId);
    }

    public async Task<UserDetailDto?> GetForDisplayAsync(string publicId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(u => u.PublicId == publicId)
            .Select(u => new UserDetailDto(
                u.PublicId,
                u.DisplayName,
                u.Email,
                u.CreatedAt,
                u.LastSeenAt))
            .FirstOrDefaultAsync();
    }

    public async Task<UserDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.PublicId == publicId);
    }

    public async Task<IEnumerable<UserDatabaseEntity>> GetByPublicIdsAsync(IEnumerable<string> publicIds)
    {
        var publicIdsList = publicIds.ToList();
        return await _dbSet
            .AsNoTracking()
            .Where(u => publicIdsList.Contains(u.PublicId))
            .ToListAsync();
    }

    public async Task<UserDatabaseEntity?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<UserDatabaseEntity?> GetByOAuthProviderIdAsync(string oauthProviderId)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.OAuthProviderId == oauthProviderId);
    }

    public async Task<UserDatabaseEntity?> GetByDisplayNameAsync(string displayName)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.DisplayName.ToLower() == displayName.ToLower());
    }

    public async Task<IEnumerable<UserDatabaseEntity>> SearchByDisplayNameAsync(string query, int limit)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(u => u.DisplayName.ToLower().Contains(query.ToLower()))
            .Take(limit)
            .ToListAsync();
    }
}
