namespace Snakk.Application.DTOs.Responses;

public record PostCreatedResponse(
    string PublicId,
    string Content,
    DateTime CreatedAt,
    string DiscussionId);

public record ReactionCountsResponse(
    int ThumbsUp,
    int Heart,
    int Eyes,
    int Crazy);

public record PostReactionsResponse(
    ReactionCountsResponse Counts,
    string? UserReaction);

public record EnrichedPostResponse(
    int PostNumber,
    string PublicId,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsFirstPost,
    bool IsDeleted,
    string CreatedByUserId,
    AuthorRef Author,
    ReplyToRef? ReplyTo,
    PostReactionsResponse Reactions);
