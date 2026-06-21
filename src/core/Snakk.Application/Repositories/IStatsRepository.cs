namespace Snakk.Application.Repositories;

public interface IStatsRepository
{
    /// <summary>
    /// Gets platform-wide statistics
    /// </summary>
    Task<PlatformStatsDto> GetPlatformStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets statistics for a specific hub
    /// </summary>
    Task<HubStatsDto?> GetHubStatsAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Gets statistics for a specific space
    /// </summary>
    Task<SpaceStatsDto?> GetSpaceStatsAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Gets statistics for a specific community
    /// </summary>
    Task<CommunityStatsDto?> GetCommunityStatsAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Gets statistics for a specific user
    /// </summary>
    Task<UserStatsDto?> GetUserStatsAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Gets statistics for a specific discussion
    /// </summary>
    Task<DiscussionStatsDto?> GetDiscussionStatsAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// Gets top active spaces by post count since the given cutoff
    /// </summary>
    Task<List<TopActiveSpaceDto>> GetTopActiveSpacesSinceAsync(
        DateTime since,
        string? hubId = null,
        string? communityId = null,
        int limit = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Gets spaces ordered by most recent post (no time filter)
    /// </summary>
    Task<List<LatestActiveSpaceDto>> GetLatestActiveSpacesAsync(
        string? hubId = null,
        string? communityId = null,
        int limit = 5,
        CancellationToken ct = default);
}

public record LatestActiveSpaceDto(
    string PublicId,
    string Name,
    string Slug,
    DateTime LastPostAt,
    string HubPublicId,
    string HubSlug,
    string HubName,
    string CommunitySlug);

public record TopActiveSpaceDto(
    string PublicId,
    string Name,
    string Slug,
    int PostCountToday,
    string HubPublicId,
    string HubSlug,
    string HubName,
    string CommunitySlug);

public record PlatformStatsDto(
    int HubCount,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount);

public record HubStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount,
    string? AvatarFileName = null);

public record SpaceStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int DiscussionCount,
    int ReplyCount,
    int FollowerCount,
    string? AvatarFileName = null);

public record CommunityStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int HubCount,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount,
    string? AvatarFileName = null);

public record UserStatsDto(
    string PublicId,
    string DisplayName,
    int DiscussionCount,
    int ReplyCount,
    int FollowerCount,
    string? AvatarFileName = null,
    string? Bio = null,
    string? AvatarThumbnailFileName = null,
    bool IsGlobalAdmin = false);

public record DiscussionStatsDto(
    string PublicId,
    string Title,
    int ReplyCount,
    int FollowerCount);
