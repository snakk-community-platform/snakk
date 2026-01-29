namespace Snakk.Web.Models;

public record DiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    string SpaceId,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    int PostCount = 0,
    int ReactionCount = 0,
    string? Tags = null);

public record CreateDiscussionRequest(
    string SpaceId,
    string UserId,
    string Title,
    string Slug,
    string FirstPostContent);

public record PostDto(
    int PostNumber,
    string PublicId,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsFirstPost,
    bool IsDeleted,
    string CreatedByUserId,
    PostAuthorDto Author,
    ReplyToDto? ReplyTo,
    PostReactionsDto? Reactions = null);

// Reactions data embedded in post response
public record PostReactionsDto(
    ReactionCountsDto Counts,
    string? UserReaction);

public record ReactionCountsDto(
    int ThumbsUp,
    int Heart,
    int Eyes);

// Author info embedded in post response
public record PostAuthorDto(
    string PublicId,
    string DisplayName,
    string? AvatarUrl,
    string? Role, // "admin", "mod", or null
    bool IsDeleted);

// Quoted snippet when replying to another post
public record ReplyToDto(
    string PostId,
    string AuthorName,
    string ContentSnippet); // First ~100 chars of content

public record CreatePostRequest(
    string DiscussionId,
    string UserId,
    string Content,
    string? ReplyToPostId);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int Offset,
    int PageSize,
    bool HasMoreItems,
    string? NextCursor = null);

// Recent discussion with space/hub/community info for front page
public record RecentDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    RecentDiscussionSpaceDto Space,
    RecentDiscussionHubDto Hub,
    RecentDiscussionCommunityDto Community,
    RecentDiscussionAuthorDto Author,
    int PostCount,
    int ReactionCount,
    string[] Tags);

public record RecentDiscussionSpaceDto(
    string PublicId,
    string Slug,
    string Name);

public record RecentDiscussionHubDto(
    string PublicId,
    string Slug,
    string Name);

public record RecentDiscussionCommunityDto(
    string PublicId,
    string Slug,
    string Name);

public record RecentDiscussionAuthorDto(
    string PublicId,
    string DisplayName,
    string? AvatarFileName);
