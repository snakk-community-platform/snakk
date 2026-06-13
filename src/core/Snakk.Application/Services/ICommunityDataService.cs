namespace Snakk.Application.Services;

/// <summary>
/// Thin read-only data-access seam for community-specific queries that would otherwise
/// require a direct SnakkDbContext reference in Snakk.Api.
/// All cache keys that carry write-path invalidation elsewhere are owned here;
/// the implementations must use the identical key strings so existing invalidation
/// handlers (e.g. community-meta:*) continue to work.
/// </summary>
public interface ICommunityDataService
{
    /// <summary>
    /// Returns denormalized discussion and post counts for the given community public IDs.
    /// </summary>
    Task<IReadOnlyDictionary<string, CommunityCountsDto>> GetCountsByPublicIdsAsync(
        IEnumerable<string> publicIds,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the cached community-meta fields used to populate gRPC CommunityInfo responses.
    /// Cache key: <c>community-meta:{publicId}</c> — write-path invalidation is handled by
    /// the community-management write paths.
    /// </summary>
    Task<CommunityMetaDto?> GetCommunityMetaAsync(string publicId, CancellationToken ct = default);
}

public record CommunityCountsDto(int DiscussionCount, int PostCount);

public record CommunityMetaDto(
    bool HasRules,
    string? RulesRevision,
    string? TeamRevision,
    bool IsRestricted,
    bool Require2FA);
