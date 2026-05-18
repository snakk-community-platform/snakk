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

    public async Task OnGetAsync(string scopeType, string scopeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=10";

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, apiClient);
        var adultSuffix = viewerAllowsAdult ? "adult" : "safe";
        var cacheKey = $"trending-discussions:{scopeType}:{scopeId}:{adultSuffix}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () =>
        {
            return scopeType switch
            {
                "hub" => apiClient.GetTopActiveDiscussionsTodayAsync(hubId: scopeId, viewerAllowsAdult: viewerAllowsAdult),
                "space" => apiClient.GetTopActiveDiscussionsTodayAsync(spaceId: scopeId, viewerAllowsAdult: viewerAllowsAdult),
                "community" => apiClient.GetTopActiveDiscussionsTodayAsync(communityId: scopeId, viewerAllowsAdult: viewerAllowsAdult),
                _ => apiClient.GetTopActiveDiscussionsTodayAsync(viewerAllowsAdult: viewerAllowsAdult)
            };
        });
        Discussions = result.Value;
        CacheSource = result.Source;
    }
}
