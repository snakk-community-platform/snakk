namespace Snakk.Application.DTOs.Responses;

public record PostCreatedResponse(
    string PublicId,
    string Content,
    DateTime CreatedAt,
    string DiscussionId);

public record PostReactionsResponse(
    Dictionary<string, int> Counts,
    List<string> UserReactions);

public record EnrichedPostResponse(
    int PostNumber,
    string PublicId,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsFirstPost,
    bool IsDeleted,
    bool HasCodeBlock,
    string CreatedByUserId,
    AuthorRef Author,
    ReplyToRef? ReplyTo,
    PostReactionsResponse Reactions);
