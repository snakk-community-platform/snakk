using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Statistics;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class TrendingSpacesModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public TopActiveSpacesList? Spaces { get; set; }
    public string CacheSource { get; set; } = "unknown";
    public ICommunityContext Community => communityContext;

    public async Task OnGetAsync(string scopeType, string scopeId)
    {
        Response.Headers.CacheControl = "public, max-age=10";

        var cacheKey = $"trending-spaces:{scopeType}:{scopeId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () =>
        {
            return scopeType switch
            {
                "hub" => apiClient.GetTopActiveSpacesTodayAsync(hubId: scopeId),
                "community" => apiClient.GetTopActiveSpacesTodayAsync(communityId: scopeId),
                _ => apiClient.GetTopActiveSpacesTodayAsync()
            };
        });
        Spaces = result.Value;
        CacheSource = result.Source;
    }
}
