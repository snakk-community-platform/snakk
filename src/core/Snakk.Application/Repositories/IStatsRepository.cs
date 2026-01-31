namespace Snakk.Application.Repositories;

public interface IStatsRepository
{
    /// <summary>
    /// Gets platform-wide statistics
    /// </summary>
    Task<PlatformStatsDto> GetPlatformStatsAsync();

    /// <summary>
    /// Gets statistics for a specific hub
    /// </summary>
    Task<HubStatsDto?> GetHubStatsAsync(string publicId);

    /// <summary>
    /// Gets statistics for a specific space
    /// </summary>
    Task<SpaceStatsDto?> GetSpaceStatsAsync(string publicId);

    /// <summary>
    /// Gets statistics for a specific community
    /// </summary>
    Task<CommunityStatsDto?> GetCommunityStatsAsync(string publicId);

    /// <summary>
    /// Gets statistics for a specific user
    /// </summary>
    Task<UserStatsDto?> GetUserStatsAsync(string publicId);

    /// <summary>
    /// Gets statistics for a specific discussion
    /// </summary>
    Task<DiscussionStatsDto?> GetDiscussionStatsAsync(string publicId);

    /// <summary>
    /// Gets top active spaces by post count for today
    /// </summary>
    Task<List<TopActiveSpaceDto>> GetTopActiveSpacesTodayAsync(
        string? hubId = null,
        string? communityId = null,
        int limit = 5);
}

public record TopActiveSpaceDto(
    string PublicId,
    string Name,
    string Slug,
    int PostCountToday,
    string HubPublicId,
    string HubSlug,
    string HubName);

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
    int ReplyCount);

public record SpaceStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int DiscussionCount,
    int ReplyCount,
    int FollowerCount);

public record CommunityStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int HubCount,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount);

public record UserStatsDto(
    string PublicId,
    string DisplayName,
    int DiscussionCount,
    int ReplyCount,
    int FollowerCount);

public record DiscussionStatsDto(
    string PublicId,
    string Title,
    int ReplyCount,
    int FollowerCount);
