using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Statistics;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class LatestContributorsModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public LatestContributorsList? Contributors { get; set; }
    public string CacheSource { get; set; } = "unknown";
    public ICommunityContext Community => communityContext;

    public async Task OnGetAsync(string scopeType, string scopeId)
    {
        Response.Headers.CacheControl = "public, max-age=10";

        var cacheKey = $"latest-contributors:{scopeType}:{scopeId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => scopeType switch
        {
            "hub"       => apiClient.GetLatestContributorsAsync(hubId: scopeId),
            "space"     => apiClient.GetLatestContributorsAsync(spaceId: scopeId),
            "community" => apiClient.GetLatestContributorsAsync(communityId: scopeId),
            _           => apiClient.GetLatestContributorsAsync()
        });

        Contributors = result.Value;
        CacheSource = result.Source;
    }
}
