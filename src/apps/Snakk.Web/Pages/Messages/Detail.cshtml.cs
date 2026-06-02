using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Models.Bff;
using Snakk.Web.Services;
using System.Security.Claims;

namespace Snakk.Web.Pages.Messages;

public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private const int PageSize = 30;

    public BffDmConversationResponse? Conversation { get; set; }
    public List<BffDmMessageResponse> Messages { get; set; } = [];
    public bool HasMoreMessages { get; set; }
    public string ConversationId { get; set; } = "";
    public string CurrentUserPublicId { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Configuration.GetValue<bool>("Features:PrivateMessagingEnabled"))
            return NotFound();

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return RedirectToPage("/Auth/Login", new { returnUrl = $"/messages/{conversationId}" });

        ConversationId = conversationId;
        CurrentUserPublicId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        var convResult = await apiClient.GetDmConversationAsync(conversationId, cancellationToken);
        if (convResult is null || !convResult.Found)
            return NotFound();

        Conversation = convResult.Conversation is null ? null :
            new BffDmConversationResponse(
                convResult.Conversation.PublicId,
                new BffDmUserResponse(
                    convResult.Conversation.OtherUser.PublicId,
                    convResult.Conversation.OtherUser.DisplayName,
                    SnakkUrlHelper.UserAvatarThumbnail(
                        convResult.Conversation.OtherUser.PublicId,
                        avatarThumbnailFileName: convResult.Conversation.OtherUser.HasAvatarThumbnailFileName
                            ? convResult.Conversation.OtherUser.AvatarThumbnailFileName
                            : null)),
                convResult.Conversation.HasLastMessageExcerpt ? convResult.Conversation.LastMessageExcerpt : null,
                convResult.Conversation.LastMessageAt?.ToDateTime().ToString("O") ?? DateTime.UtcNow.ToString("O"),
                convResult.Conversation.IsPinned);

        var msgResult = await apiClient.GetDmMessagesAsync(conversationId, 0, PageSize, cancellationToken);
        if (msgResult is not null)
        {
            Messages = msgResult.Items.Select(m => new BffDmMessageResponse(
                m.PublicId, m.SenderPublicId, m.Content,
                m.CreatedAt.ToDateTime().ToString("O"), m.IsMine)).ToList();
            HasMoreMessages = msgResult.HasMoreItems;
        }

        // Mark as read (fire and forget)
        _ = apiClient.MarkDmAsReadAsync(conversationId, cancellationToken);

        return Page();
    }
}
