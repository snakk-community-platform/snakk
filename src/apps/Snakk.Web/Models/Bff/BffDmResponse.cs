namespace Snakk.Web.Models.Bff;

public record BffDmUserResponse(
    string PublicId,
    string DisplayName,
    string AvatarUrl,
    string? Slug = null);

public record BffDmConversationResponse(
    string PublicId,
    BffDmUserResponse OtherUser,
    string? LastMessageExcerpt,
    string LastMessageAt,
    bool IsPinned = false);

public record BffDmConversationsResponse(
    List<BffDmConversationResponse> Items,
    int Offset,
    int PageSize,
    bool HasMoreItems);

public record BffDmMessageResponse(
    string PublicId,
    string SenderPublicId,
    string Content,
    string CreatedAt,
    bool IsMine);

public record BffDmMessagesResponse(
    List<BffDmMessageResponse> Items,
    int Offset,
    int PageSize,
    bool HasMoreItems);

public record BffDmSendRequest(string Content);
public record BffDmCreateRequest(string RecipientPublicId);
public record BffDmUnreadCountResponse(int Count);
public record BffDmDeleteMessagesRequest(List<string> MessageIds, bool DeleteForAll);
public record BffDmPinRequest(bool IsPinned);
