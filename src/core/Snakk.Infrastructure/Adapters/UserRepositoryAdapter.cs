namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;

public class UserRepositoryAdapter(
    Infrastructure.Database.Repositories.IUserRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IUserRepository
{
    private readonly Infrastructure.Database.Repositories.IUserRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<User?> GetByIdAsync(int id)
    {
        var entity = await _databaseRepository.GetByIdAsync(id);
        return entity?.FromPersistence();
    }

    public async Task<User?> GetByPublicIdAsync(UserId publicId)
    {
        var entity = await _databaseRepository.GetForUpdateAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<User>> GetByPublicIdsAsync(IEnumerable<UserId> publicIds)
    {
        var publicIdStrings = publicIds.Select(id => id.Value).ToList();
        var entities = await _databaseRepository.GetByPublicIdsAsync(publicIdStrings);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var entity = await _databaseRepository.GetByEmailAsync(email);
        return entity?.FromPersistence();
    }

    public async Task<User?> GetByOAuthProviderIdAsync(string oauthProviderId)
    {
        var entity = await _databaseRepository.GetByOAuthProviderIdAsync(oauthProviderId);
        return entity?.FromPersistence();
    }

    public async Task<User?> GetByDisplayNameAsync(string displayName)
    {
        var entity = await _databaseRepository.GetByDisplayNameAsync(displayName);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<User>> SearchByDisplayNameAsync(string query, int limit)
    {
        var entities = await _databaseRepository.SearchByDisplayNameAsync(query, limit);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        var entities = await _databaseRepository.GetAllAsync();
        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddAsync(User user)
    {
        var entity = user.ToPersistence();
        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        var entity = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == user.PublicId.Value);
        if (entity == null)
            throw new InvalidOperationException($"User with PublicId '{user.PublicId}' not found");

        entity.DisplayName = user.DisplayName;
        entity.Email = user.Email;
        entity.LastModifiedAt = user.LastModifiedAt;
        entity.LastSeenAt = user.LastSeenAt;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }
}
