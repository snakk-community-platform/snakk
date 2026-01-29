namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface ICommunityDatabaseRepository : IGenericDatabaseRepository<CommunityDatabaseEntity>
{
    Task<CommunityDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<CommunityDatabaseEntity?> GetBySlugAsync(string slug);
    Task<CommunityDatabaseEntity?> GetByDomainAsync(string domain);
    Task<PagedResult<CommunityListDto>> GetPublicListedAsync(int offset, int pageSize);
    Task<PagedResult<CommunityListDto>> GetForPlatformFeedAsync(int offset, int pageSize);
}

public record CommunityListDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    string Visibility,
    bool ExposeToPlatformFeed,
    DateTime CreatedAt);

public record CommunityDetailDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    string Visibility,
    bool ExposeToPlatformFeed,
    DateTime CreatedAt,
    DateTime? LastModifiedAt);
