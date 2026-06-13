namespace Snakk.Application.Services;

/// <summary>
/// Thin read-only data-access seam for hub-specific queries that would otherwise
/// require a direct SnakkDbContext reference in Snakk.Api.
/// All cache keys that carry write-path invalidation elsewhere are owned here;
/// the implementations must use the identical key strings so existing invalidation
/// handlers (e.g. hub-meta:*) continue to work.
/// </summary>
public interface IHubDataService
{
    /// <summary>
    /// Returns denormalized space, discussion, and post counts for the given hub public IDs.
    /// </summary>
    Task<IReadOnlyDictionary<string, HubCountsDto>> GetCountsByPublicIdsAsync(
        IEnumerable<string> publicIds,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the cached hub-meta fields used to populate gRPC HubInfo responses.
    /// Cache key: <c>hub-meta:{publicId}</c> — write-path invalidation is handled by
    /// the hub-management write paths.
    /// </summary>
    Task<HubMetaDto?> GetHubMetaAsync(string publicId, CancellationToken ct = default);
}

public record HubCountsDto(int SpaceCount, int DiscussionCount, int PostCount);

public record HubMetaDto(
    bool HasRules,
    string? RulesRevision,
    bool ParentCommunityHasRules,
    string? TeamRevision,
    bool IsRestricted,
    string? CommunitySlug,
    bool Require2FA);
