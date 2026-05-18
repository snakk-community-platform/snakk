using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Statistics;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class LatestSpacesModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public LatestSpacesList? Spaces { get; set; }
    public string CacheSource { get; set; } = "unknown";
    public ICommunityContext Community => communityContext;

    public async Task OnGetAsync(string scopeType, string scopeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=10";

        var cacheKey = $"latest-spaces:{scopeType}:{scopeId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => scopeType switch
        {
            "hub"       => apiClient.GetLatestActiveSpacesAsync(hubId: scopeId),
            "community" => apiClient.GetLatestActiveSpacesAsync(communityId: scopeId),
            _           => apiClient.GetLatestActiveSpacesAsync()
        });

        Spaces = result.Value;
        CacheSource = result.Source;
    }
}
