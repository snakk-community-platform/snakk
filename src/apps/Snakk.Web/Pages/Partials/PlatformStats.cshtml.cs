using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class PlatformStatsModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache) : PageModel
{
    public int SpaceCount { get; set; }
    public int DiscussionCount { get; set; }
    public int ReplyCount { get; set; }
    public string CacheSource { get; set; } = "unknown";

    public async Task OnGetAsync(string scopeType, string scopeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=10";

        if (scopeType == "community")
        {
            var result = await prefetchCache.GetOrFetchAsync(
                $"platform-stats:community:{scopeId}",
                () => apiClient.GetCommunityStatsAsync(scopeId));
            var stats = result.Value;
            if (stats is not null)
            {
                SpaceCount = stats.SpaceCount;
                DiscussionCount = stats.DiscussionCount;
                ReplyCount = stats.ReplyCount;
            }
            CacheSource = result.Source;
        }
        else
        {
            var result = await prefetchCache.GetOrFetchAsync(
                "platform-stats:platform:global",
                () => apiClient.GetPlatformStatsAsync());
            var stats = result.Value;
            if (stats is not null)
            {
                SpaceCount = stats.SpaceCount;
                DiscussionCount = stats.DiscussionCount;
                ReplyCount = stats.ReplyCount;
            }
            CacheSource = result.Source;
        }
    }
}
