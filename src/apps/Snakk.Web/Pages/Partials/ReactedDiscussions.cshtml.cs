using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class ReactedDiscussionsModel(
    SnakkApiClient apiClient,
    ICommunityContext communityContext) : PageModel
{
    public ICommunityContext Community => communityContext;
    public List<ReactedDiscussionVM> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int Offset { get; set; }
    public int NextOffset { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return Content("", "text/html");

        pageSize = Math.Clamp(pageSize, 1, 20);
        Offset = offset;

        try
        {
            var result = await apiClient.GetMyReactedDiscussionsAsync(offset, pageSize);
            if (result != null)
            {
                var reactions = result.Items.Select(p => new ReactedPostVM(
                    p.PublicId,
                    p.DiscussionPublicId,
                    p.DiscussionTitle,
                    p.DiscussionSlug,
                    p.SpaceSlug,
                    p.SpaceName,
                    p.HubSlug,
                    p.HubName,
                    p.CommunitySlug,
                    p.AuthorPublicId,
                    p.AuthorDisplayName,
                    p.HasAuthorAvatarFileName ? p.AuthorAvatarFileName : null,
                    p.ContentExcerpt,
                    p.PostCreatedAt?.ToDateTime() ?? DateTime.UtcNow,
                    p.ReactedAt?.ToDateTime() ?? DateTime.UtcNow,
                    p.ReactionType)).ToList();

                Items = await ReactedDiscussionLoader.LoadAsync(apiClient, reactions);
                HasMoreItems = result.HasMoreItems;
                NextOffset = offset + reactions.Count;
            }
        }
        catch { }

        return Page();
    }
}

public record ReactedDiscussionVM(ReactedPostVM Reaction, RecentDiscussionInfo Discussion);

internal static class ReactedDiscussionLoader
{
    public static async Task<List<ReactedDiscussionVM>> LoadAsync(
        SnakkApiClient apiClient,
        IReadOnlyList<ReactedPostVM> reactions)
    {
        if (reactions.Count == 0) return [];

        var ids = reactions.Select(r => r.DiscussionPublicId).Distinct().ToList();
        var fetched = await apiClient.GetRecentDiscussionsByIdsAsync(ids);
        if (fetched == null) return [];

        var byId = fetched.Items.ToDictionary(d => d.PublicId);
        return reactions
            .Where(r => byId.ContainsKey(r.DiscussionPublicId))
            .Select(r => new ReactedDiscussionVM(r, byId[r.DiscussionPublicId]))
            .ToList();
    }
}
