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

    public async Task OnGetAsync(string spaceId, string rev = "")
    {
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        // Derive parent slugs and rule flags from the space
        var space = await apiClient.GetSpaceAsync(spaceId);
        HubSlug = space?.HubSlug ?? "";
        CommunitySlug = space?.CommunitySlug ?? "";
        ParentHubHasRules = space?.ParentHubHasRules ?? false;
        ParentCommunityHasRules = space?.ParentCommunityHasRules ?? false;

        var cacheKey = $"space-rules:{spaceId}";

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => apiClient.GetSpaceRulesAsync(spaceId)!);
        Rules = result.Value;
        CacheSource = result.Source;
    }
}
