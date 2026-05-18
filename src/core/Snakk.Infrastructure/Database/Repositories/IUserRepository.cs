namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserRepository : IGenericDatabaseRepository<UserDatabaseEntity>
{
    Task<UserDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default);
    Task<UserDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default);
    Task<UserDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<IEnumerable<UserDatabaseEntity>> GetByPublicIdsAsync(IEnumerable<string> publicIds, CancellationToken ct = default);
    Task<UserDatabaseEntity?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserDatabaseEntity?> GetByOAuthProviderIdAsync(string oauthProviderId, CancellationToken ct = default);
    Task<UserDatabaseEntity?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default);
    Task<IEnumerable<UserDatabaseEntity>> SearchByDisplayNameAsync(string query, int limit, CancellationToken ct = default);
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
