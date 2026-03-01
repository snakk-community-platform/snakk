using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Space;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class SpaceRulesModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public SpaceRulesResponse? Rules { get; set; }
    public string CacheSource { get; set; } = "unknown";
    public ICommunityContext Community => communityContext;
    public string HubSlug { get; set; } = string.Empty;
    public string CommunitySlug { get; set; } = string.Empty;
    public bool ParentHubHasRules { get; set; }
    public bool ParentCommunityHasRules { get; set; }

    public async Task OnGetAsync(string spaceId, string hubSlug = "", string communitySlug = "", bool parentHubHasRules = false, bool parentCommunityHasRules = false, string rev = "")
    {
        Response.Headers.CacheControl = "public, max-age=86400";

        HubSlug = hubSlug;
        CommunitySlug = communitySlug;
        ParentHubHasRules = parentHubHasRules;
        ParentCommunityHasRules = parentCommunityHasRules;

        var cacheKey = $"space-rules:{spaceId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => apiClient.GetSpaceRulesAsync(spaceId)!);
        Rules = result.Value;
        CacheSource = result.Source;
    }
}
