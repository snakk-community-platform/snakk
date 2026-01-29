namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserRepository : IGenericDatabaseRepository<UserDatabaseEntity>
{
    Task<UserDatabaseEntity?> GetForUpdateAsync(string publicId);
    Task<UserDetailDto?> GetForDisplayAsync(string publicId);
    Task<UserDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<IEnumerable<UserDatabaseEntity>> GetByPublicIdsAsync(IEnumerable<string> publicIds);
    Task<UserDatabaseEntity?> GetByEmailAsync(string email);
    Task<UserDatabaseEntity?> GetByOAuthProviderIdAsync(string oauthProviderId);
    Task<UserDatabaseEntity?> GetByDisplayNameAsync(string displayName);
    Task<IEnumerable<UserDatabaseEntity>> SearchByDisplayNameAsync(string query, int limit);
}

public record UserListDto(
    string PublicId,
    string DisplayName,
    DateTime CreatedAt,
    DateTime? LastSeenAt);

public record UserDetailDto(
    string PublicId,
    string DisplayName,
    string? Email,
    DateTime CreatedAt,
    DateTime? LastSeenAt);
