namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface ICommunityDatabaseRepository : IGenericDatabaseRepository<CommunityDatabaseEntity>
{
    Task<CommunityDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<CommunityDatabaseEntity?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<CommunityDatabaseEntity?> GetByDomainAsync(string domain, CancellationToken ct = default);
    Task<PagedResult<CommunityListDto>> GetPublicListedAsync(int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<CommunityListDto>> GetForPlatformFeedAsync(int offset, int pageSize, CancellationToken ct = default);
}

public record CommunityListDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    int VisibilityId,
    bool ExposeToPlatformFeed,
    DateTime CreatedAt,
    string? AvatarFileName);

public record CommunityDetailDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    int VisibilityId,
    bool ExposeToPlatformFeed,
    DateTime CreatedAt,
    DateTime? LastModifiedAt);
