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

    public async Task OnGetAsync(string hubId, string communitySlug = "", bool parentCommunityHasRules = false, string rev = "")
    {
        Response.Headers.CacheControl = "public, max-age=86400";

        CommunitySlug = communitySlug;
        ParentCommunityHasRules = parentCommunityHasRules;

        var cacheKey = $"hub-rules:{hubId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => apiClient.GetHubRulesAsync(hubId)!);
        Rules = result.Value;
        CacheSource = result.Source;
    }
}
