namespace Snakk.Application.Services;

/// <summary>
/// Caches the immutable parent-chain integer IDs for discussions, spaces, and hubs.
/// Since the hierarchy never changes after creation (entities can't be reparented),
/// entries are cached indefinitely and only evicted under memory pressure.
/// </summary>
public interface IEntityHierarchyCacheService
{
    Task<DiscussionHierarchy?> GetDiscussionHierarchyAsync(string publicId, CancellationToken ct = default);
    Task<SpaceHierarchy?> GetSpaceHierarchyAsync(string publicId, CancellationToken ct = default);
    Task<HubHierarchy?> GetHubHierarchyAsync(string publicId, CancellationToken ct = default);
    Task<int?> GetCommunityIdAsync(string publicId, CancellationToken ct = default);
}

/// <param name="SpaceId">Internal Space.Id</param>
/// <param name="HubId">Internal Hub.Id (via Space.HubId)</param>
/// <param name="CommunityId">Internal Community.Id (via Hub.CommunityId)</param>
public record DiscussionHierarchy(int SpaceId, int HubId, int CommunityId);

/// <param name="Id">Internal Space.Id</param>
/// <param name="HubId">Internal Hub.Id</param>
/// <param name="CommunityId">Internal Community.Id</param>
public record SpaceHierarchy(int Id, int HubId, int CommunityId);

/// <param name="Id">Internal Hub.Id</param>
/// <param name="CommunityId">Internal Community.Id</param>
public record HubHierarchy(int Id, int CommunityId);
