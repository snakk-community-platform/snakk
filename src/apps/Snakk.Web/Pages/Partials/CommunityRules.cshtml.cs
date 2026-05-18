using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Community;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class CommunityRulesModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache) : PageModel
{
    public CommunityRulesResponse? Rules { get; set; }
    public string CacheSource { get; set; } = "unknown";

    public async Task OnGetAsync(string communityId, string rev = "", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        var cacheKey = $"community-rules:{communityId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => apiClient.GetCommunityRulesAsync(communityId)!);
        Rules = result.Value;
        CacheSource = result.Source;
    }
}
