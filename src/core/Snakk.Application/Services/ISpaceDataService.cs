namespace Snakk.Application.Services;

/// <summary>
/// Thin read-only data-access seam for space-specific queries that would otherwise
/// require a direct SnakkDbContext reference in Snakk.Api.
/// All cache keys that carry write-path invalidation elsewhere are owned here;
/// the implementations must use the identical key strings so existing invalidation
/// handlers (e.g. space-meta:*) continue to work.
/// </summary>
public interface ISpaceDataService
{
    /// <summary>
    /// Returns the Discord invite URL for the given space, or <c>null</c> if not configured.
    /// </summary>
    Task<string?> GetDiscordInviteUrlAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Returns the cached space-meta fields used to populate gRPC SpaceInfo responses.
    /// Cache key: <c>space-meta:{publicId}</c> — write-path invalidation is handled by
    /// the space-management write paths.
    /// </summary>
    Task<SpaceMetaDto?> GetSpaceMetaAsync(string publicId, CancellationToken ct = default);
}

public record SpaceMetaDto(
    bool HasRules,
    string? RulesRevision,
    bool ParentHubHasRules,
    bool ParentCommunityHasRules,
    string? TeamRevision,
    bool IsRestricted,
    List<int> AllowedTypes,
    string? HubSlug,
    string? CommunitySlug,
    int DiscussionCount,
    int ReplyCount,
    SpaceLatestDiscussionDto? LatestDiscussion,
    bool Require2FA,
    bool AllowAnonymousReading);

public record SpaceLatestDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime LastActivityAt,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    int PostCount);
