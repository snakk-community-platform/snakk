using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Community;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class SiteRulesModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache) : PageModel
{
    public SiteRulesResponse? Rules { get; set; }
    public string CacheSource { get; set; } = "unknown";

    public async Task OnGetAsync(string rev = "", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        var cacheKey = "site-rules";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => apiClient.GetSiteRulesAsync()!);
        Rules = result.Value;
        CacheSource = result.Source;
    }
}
