using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.Partials;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class ReactionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private const int PageSize = 10;

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "discussions";

    public bool IsAuthenticated { get; set; }
    public List<ReactedPostVM> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        if (!IsAuthenticated) return Page();

        try
        {
            var result = Tab == "posts"
                ? await _apiClient.GetMyReactedPostsAsync(0, PageSize)
                : await _apiClient.GetMyReactedDiscussionsAsync(0, PageSize);

            if (result != null)
            {
                Items = result.Items.Select(p => new ReactedPostVM(
                    p.PublicId,
                    p.DiscussionPublicId,
                    p.DiscussionTitle,
                    p.DiscussionSlug,
                    p.SpaceSlug,
                    p.HubSlug,
                    p.CommunitySlug,
                    p.AuthorPublicId,
                    p.AuthorDisplayName,
                    p.HasAuthorAvatarFileName ? p.AuthorAvatarFileName : null,
                    p.ContentExcerpt,
                    p.ReactedAt?.ToDateTime() ?? DateTime.UtcNow,
                    p.ReactionType)).ToList();
                HasMoreItems = result.HasMoreItems;
                NextOffset = Items.Count;
            }
        }
        catch { }

        return Page();
    }
}
