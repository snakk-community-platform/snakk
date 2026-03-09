namespace Snakk.Application.DTOs.Responses;

public record DiscussionResponse(
    string PublicId,
    string Title,
    string Slug,
    string Type,
    string SpaceId,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked);

public record DiscussionCreatedResponse(
    string PublicId,
    string Title,
    string Slug,
    string Type,
    DateTime CreatedAt);

public record RecentDiscussionResponse(
    string PublicId,
    string Title,
    string Slug,
    string Type,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    EntityRef Space,
    EntityRef Hub,
    EntityRef Community,
    AuthorRef Author,
    int PostCount,
    int ReactionCount,
    string[]? Tags);

public record DiscussionBySpaceResponse(
    string PublicId,
    string SpaceId,
    string Title,
    string Slug,
    string Type,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    int PostCount,
    int ReactionCount,
    AuthorRef Author,
    string[]? Tags);

public record TopActiveDiscussionResponse(
    string PublicId,
    string Title,
    string Slug,
    int PostCountToday,
    EntityRef Space,
    EntityRef Hub,
    AuthorRef Author);

public record TopActiveDiscussionsResponse(IEnumerable<TopActiveDiscussionResponse> Items);

public record DiscussionPreviewResponse(string Content);

public record DiscussionStatsResponse(
    string PublicId,
    string Title,
    int ReplyCount,
    int FollowerCount);

public record PostNumberResponse(int PostNumber);
