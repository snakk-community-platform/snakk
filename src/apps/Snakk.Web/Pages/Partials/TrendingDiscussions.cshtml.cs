using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Statistics;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class TrendingDiscussionsModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public TopActiveDiscussionsList? Discussions { get; set; }
    public string CacheSource { get; set; } = "unknown";
    public ICommunityContext Community => communityContext;

    public async Task OnGetAsync(string scopeType, string scopeId)
    {
        Response.Headers.CacheControl = "public, max-age=10";

        var cacheKey = $"trending-discussions:{scopeType}:{scopeId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () =>
        {
            return scopeType switch
            {
                "hub" => apiClient.GetTopActiveDiscussionsTodayAsync(hubId: scopeId),
                "space" => apiClient.GetTopActiveDiscussionsTodayAsync(spaceId: scopeId),
                "community" => apiClient.GetTopActiveDiscussionsTodayAsync(communityId: scopeId),
                _ => apiClient.GetTopActiveDiscussionsTodayAsync()
            };
        });
        Discussions = result.Value;
        CacheSource = result.Source;
    }
}
