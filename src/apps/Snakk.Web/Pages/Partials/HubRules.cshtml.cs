using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Hub;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class HubRulesModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public HubRulesResponse? Rules { get; set; }
    public string CacheSource { get; set; } = "unknown";
    public ICommunityContext Community => communityContext;
    public string CommunitySlug { get; set; } = string.Empty;
    public bool ParentCommunityHasRules { get; set; }

    public async Task OnGetAsync(string hubId, string rev = "", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        // Derive community slug and rule flags from the hub
        var hub = await apiClient.GetHubAsync(hubId);
        CommunitySlug = hub?.CommunitySlug ?? "";
        ParentCommunityHasRules = hub?.ParentCommunityHasRules ?? false;

        var cacheKey = $"hub-rules:{hubId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => apiClient.GetHubRulesAsync(hubId)!);
        Rules = result.Value;
        CacheSource = result.Source;
    }
}
