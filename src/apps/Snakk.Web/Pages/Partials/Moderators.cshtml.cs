using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Moderation;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class ModeratorsModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache) : PageModel
{
    public GetModeratorsResponse? Moderators { get; set; }
    public string ModeratorsPageUrl { get; set; } = "/";
    public string CacheSource { get; set; } = "unknown";

    public async Task OnGetAsync(
        string scopeType,
        string scopeId,
        string moderatorsUrl = "/",
        string rev = "")
    {
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        ModeratorsPageUrl = moderatorsUrl;

        var cacheKey = $"moderators:{scopeType}:{scopeId}";

        var result = await prefetchCache.GetOrFetchAsync(
            cacheKey,
            () => apiClient.GetModeratorsAsync(scopeType, scopeId)!);
        Moderators = result.Value;
        CacheSource = result.Source;
    }
}
