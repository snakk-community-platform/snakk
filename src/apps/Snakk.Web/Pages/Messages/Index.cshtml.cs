using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Dm;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Messages;

public class IndexModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private const int PageSize = 20;

    public List<ConversationInfo> Conversations { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Configuration.GetValue<bool>("Features:PrivateMessagingEnabled"))
            return NotFound();

        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return RedirectToPage("/Auth/Login", new { returnUrl = "/messages" });

        var result = await apiClient.GetDmConversationsAsync(offset, PageSize, cancellationToken);
        if (result is not null)
        {
            Conversations = result.Items.ToList();
            HasMoreItems = result.HasMoreItems;
            NextOffset = offset + PageSize;
        }

        return Page();
    }
}
