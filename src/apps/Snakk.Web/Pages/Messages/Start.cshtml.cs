using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Messages;

public class StartModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public async Task<IActionResult> OnGetAsync(
        string recipient,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Configuration.GetValue<bool>("Features:PrivateMessagingEnabled"))
            return NotFound();

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return RedirectToPage("/Auth/Login", new { returnUrl = $"/messages/start?recipient={recipient}" });

        if (string.IsNullOrWhiteSpace(recipient))
            return RedirectToPage("/Messages/Index");

        var result = await apiClient.GetOrCreateDmConversationAsync(recipient, cancellationToken);
        if (result is null || !result.Found || result.Conversation is null)
            return RedirectToPage("/Messages/Index");

        return RedirectToPage("/Messages/Detail", new { conversationId = result.Conversation.PublicId });
    }
}
