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
    public string Mode { get; set; } = "active";
    public string Period { get; set; } = "week";
    public ICommunityContext Community => communityContext;

    public string SectionLabel => (Mode, Period) switch
    {
        ("trending", _)         => "posts this week",
        ("top", "day")          => "posts today",
        ("top", "month")        => "posts this month",
        ("top", "year")         => "posts this year",
        ("top", "all_time")     => "all-time posts",
        ("top", _)              => "posts this week",
        _                       => "posts today"
    };

    public async Task OnGetAsync(string scopeType, string scopeId, string mode = "active", string period = "week", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=10";
        Mode = mode;
        Period = period;

        var cacheKey = mode switch
        {
            "trending" => $"trending-spaces-7d:{scopeType}:{scopeId}",
            "top"      => $"top-spaces:{period}:{scopeType}:{scopeId}",
            _          => $"active-spaces:{scopeType}:{scopeId}"
        };

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => (mode, scopeType) switch
        {
            ("trending", "hub")       => apiClient.GetTrendingSpacesAsync(hubId: scopeId),
            ("trending", "community") => apiClient.GetTrendingSpacesAsync(communityId: scopeId),
            ("trending", _)           => apiClient.GetTrendingSpacesAsync(),
            ("top", "hub")            => apiClient.GetTopSpacesByPeriodAsync(period, hubId: scopeId),
            ("top", "community")      => apiClient.GetTopSpacesByPeriodAsync(period, communityId: scopeId),
            ("top", _)                => apiClient.GetTopSpacesByPeriodAsync(period),
            (_, "hub")                => apiClient.GetTopActiveSpacesTodayAsync(hubId: scopeId),
            (_, "community")          => apiClient.GetTopActiveSpacesTodayAsync(communityId: scopeId),
            _                         => apiClient.GetTopActiveSpacesTodayAsync()
        });

        Spaces = result.Value;
        CacheSource = result.Source;
    }
}
