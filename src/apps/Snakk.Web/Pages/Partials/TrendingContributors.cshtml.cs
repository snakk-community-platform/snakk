using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Statistics;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class TrendingContributorsModel(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : PageModel
{
    public TopContributorsList? Contributors { get; set; }
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

    public async Task OnGetAsync(string scopeType, string scopeId, string mode = "active", string period = "week")
    {
        Response.Headers.CacheControl = "public, max-age=10";
        Mode = mode;
        Period = period;

        var cacheKey = mode switch
        {
            "trending" => $"trending-contributors-7d:{scopeType}:{scopeId}",
            "top"      => $"top-contributors:{period}:{scopeType}:{scopeId}",
            _          => $"active-contributors:{scopeType}:{scopeId}"
        };

        var result = await prefetchCache.GetOrFetchAsync(cacheKey, () => (mode, scopeType) switch
        {
            ("trending", "hub")       => apiClient.GetTrendingContributorsAsync(hubId: scopeId),
            ("trending", "space")     => apiClient.GetTrendingContributorsAsync(spaceId: scopeId),
            ("trending", "community") => apiClient.GetTrendingContributorsAsync(communityId: scopeId),
            ("trending", _)           => apiClient.GetTrendingContributorsAsync(),
            ("top", "hub")            => apiClient.GetTopContributorsByPeriodAsync(period, hubId: scopeId),
            ("top", "space")          => apiClient.GetTopContributorsByPeriodAsync(period, spaceId: scopeId),
            ("top", "community")      => apiClient.GetTopContributorsByPeriodAsync(period, communityId: scopeId),
            ("top", _)                => apiClient.GetTopContributorsByPeriodAsync(period),
            (_, "hub")                => apiClient.GetTopContributorsTodayAsync(hubId: scopeId),
            (_, "space")              => apiClient.GetTopContributorsTodayAsync(spaceId: scopeId),
            (_, "community")          => apiClient.GetTopContributorsTodayAsync(communityId: scopeId),
            _                         => apiClient.GetTopContributorsTodayAsync()
        });

        Contributors = result.Value;
        CacheSource = result.Source;
    }
}
