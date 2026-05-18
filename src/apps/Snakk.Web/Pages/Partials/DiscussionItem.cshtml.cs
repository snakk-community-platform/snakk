using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class DiscussionItemModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IMemoryCache cache) : BasePageModel(configuration, communityContext)
{
    public DiscussionInfo? Discussion { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        string discussionId,
        string hubSlug,
        string spaceSlug,
        string authorId,
        string authorName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HubSlug = hubSlug;
        SpaceSlug = spaceSlug;
        AuthorId = authorId;
        AuthorName = authorName;

        // No per-user variation in discussion item rendering — fully shared cache
        var cacheKey = $"partial:discussion-item:{discussionId}";
        Discussion = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
            return await apiClient.GetDiscussionAsync(discussionId);
        });

        if (Discussion is null)
            return NotFound();

        return Page();
    }
}
