namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByPublicIdAsync(UserId publicId);
    Task<IEnumerable<User>> GetByPublicIdsAsync(IEnumerable<UserId> publicIds);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByOAuthProviderIdAsync(string oauthProviderId);
    Task<User?> GetByDisplayNameAsync(string displayName);
    Task<IEnumerable<User>> SearchByDisplayNameAsync(string query, int limit);
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}
