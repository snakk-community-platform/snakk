using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class ReactedPostsModel(
    SnakkApiClient apiClient,
    ICommunityContext communityContext) : PageModel
{
    public ICommunityContext Community => communityContext;
    public List<ReactedPostVM> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int Offset { get; set; }
    public int NextOffset { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 10)
    {
        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return Content("", "text/html");

        pageSize = Math.Clamp(pageSize, 1, 20);
        Offset = offset;

        try
        {
            var result = await apiClient.GetMyReactedPostsAsync(offset, pageSize);
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
                NextOffset = offset + Items.Count;
            }
        }
        catch { }

        return Page();
    }
}

public record ReactedPostVM(
    string PublicId,
    string DiscussionPublicId,
    string DiscussionTitle,
    string DiscussionSlug,
    string SpaceSlug,
    string HubSlug,
    string CommunitySlug,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string? ContentExcerpt,
    DateTime ReactedAt,
    string ReactionType)
{
    public string ReactionEmoji => ReactionType switch
    {
        "Agree"     => "👍",
        "Love"      => "❤️",
        "Funny"     => "😂",
        "Thinking"  => "🤔",
        "Watching"  => "👀",
        "Fire"      => "🔥",
        "Thanks"    => "🙏",
        "MindBlown" => "🤯",
        "ShipIt"    => "🚀",
        _           => "👍"
    };
}
