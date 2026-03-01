using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Statistics;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class TrendingContributorsModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public TopContributorsList? Contributors { get; set; }
    public string CacheSource { get; set; } = "unknown";
    public ICommunityContext Community => communityContext;

    public async Task OnGetAsync(string scopeType, string scopeId)
    {
        Response.Headers.CacheControl = "public, max-age=10";

        var cacheKey = $"trending-contributors:{scopeType}:{scopeId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () =>
        {
            return scopeType switch
            {
                "hub" => apiClient.GetTopContributorsTodayAsync(hubId: scopeId),
                "space" => apiClient.GetTopContributorsTodayAsync(spaceId: scopeId),
                "community" => apiClient.GetTopContributorsTodayAsync(communityId: scopeId),
                _ => apiClient.GetTopContributorsTodayAsync()
            };
        });
        Contributors = result.Value;
        CacheSource = result.Source;
    }
}
