namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public interface IDiscussionRepository : IGenericDatabaseRepository<DiscussionDatabaseEntity>
{
    Task<DiscussionDatabaseEntity?> GetForUpdateAsync(string publicId);
    Task<DiscussionDetailDto?> GetForDisplayAsync(string publicId);
    Task<DiscussionDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<DiscussionDatabaseEntity?> GetBySlugAsync(string slug);
    Task<IEnumerable<DiscussionDatabaseEntity>> GetBySpaceIdAsync(int spaceId);
    Task<PagedResult<DiscussionListDto>> GetPagedBySpaceIdAsync(int spaceId, int offset, int pageSize, string? cursor = null);
    Task<IEnumerable<DiscussionDatabaseEntity>> GetRecentAsync(int count);
    Task<PagedResult<RecentDiscussionDto>> GetRecentWithDetailsAsync(int offset, int pageSize, string? communityId = null, string? cursor = null);
}

public record DiscussionListDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    string CreatedByUserPublicId,
    string CreatedByUserDisplayName,
    int PostCount,
    int ReactionCount,
    string[] Tags);

public record DiscussionDetailDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    string SpacePublicId,
    string SpaceName,
    string CreatedByUserPublicId,
    string CreatedByUserDisplayName);

public record RecentDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    string SpacePublicId,
    string SpaceSlug,
    string SpaceName,
    string HubPublicId,
    string HubSlug,
    string HubName,
    string CommunityPublicId,
    string CommunitySlug,
    string CommunityName,
    string CreatedByUserPublicId,
    string CreatedByUserDisplayName,
    string? CreatedByUserAvatarFileName,
    int PostCount,
    int ReactionCount,
    string[] Tags);
