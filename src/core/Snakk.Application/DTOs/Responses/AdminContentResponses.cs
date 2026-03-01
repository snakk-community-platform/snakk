namespace Snakk.Application.DTOs.Responses;

public record ContentOverviewResponse(
    int TotalCommunities,
    int TotalHubs,
    int TotalSpaces,
    int TotalDiscussions,
    int TotalPosts,
    int PinnedDiscussions,
    int LockedDiscussions);

public record AdminCommunityListResponse(
    IEnumerable<AdminCommunityItemResponse> Communities,
    int Total,
    int Page,
    int PageSize);

public record AdminCommunityItemResponse(
    string Id,
    string Name,
    string Slug,
    string? Description,
    string Visibility,
    DateTime CreatedAt,
    int HubCount,
    int MemberCount);

public record AdminCommunityDetailResponse(
    string Id,
    string Name,
    string Slug,
    string? Description,
    string Visibility,
    DateTime CreatedAt,
    int HubCount,
    int MemberCount,
    IEnumerable<AdminHubSummaryResponse> Hubs);

public record AdminHubSummaryResponse(string Id, string Name, string Slug, int SpaceCount);

public record AdminHubListResponse(
    IEnumerable<AdminHubItemResponse> Hubs,
    int Total,
    int Page,
    int PageSize);

public record AdminHubItemResponse(
    string Id,
    string Name,
    string Slug,
    string? Description,
    string CommunityId,
    string CommunityName,
    DateTime CreatedAt,
    int SpaceCount);

public record AdminSpaceListResponse(
    IEnumerable<AdminSpaceItemResponse> Spaces,
    int Total,
    int Page,
    int PageSize);

public record AdminSpaceItemResponse(
    string Id,
    string Name,
    string Slug,
    string? Description,
    string HubId,
    string HubName,
    string CommunityId,
    string CommunityName,
    DateTime CreatedAt,
    int DiscussionCount);

public record AdminDiscussionListResponse(
    IEnumerable<AdminDiscussionItemResponse> Discussions,
    int Total,
    int Page,
    int PageSize);

public record AdminDiscussionItemResponse(
    string Id,
    string Title,
    string Slug,
    string SpaceId,
    string SpaceName,
    string HubId,
    string HubName,
    string CommunityId,
    string CommunityName,
    string AuthorId,
    string AuthorName,
    int PostCount,
    bool IsPinned,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime? LastActivityAt);

public record AdminDiscussionDetailResponse(
    string Id,
    string Title,
    string Slug,
    string SpaceId,
    string SpaceName,
    string HubId,
    string HubName,
    string CommunityId,
    string CommunityName,
    string AuthorId,
    string AuthorName,
    int PostCount,
    int ReactionCount,
    bool IsPinned,
    bool IsLocked,
    string? Tags,
    DateTime CreatedAt,
    DateTime? LastActivityAt);
