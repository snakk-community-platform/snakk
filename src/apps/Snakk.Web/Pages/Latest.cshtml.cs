using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Discussion;

namespace Snakk.Web.Pages;

[OutputCache(PolicyName = "AnonymousPage")]
public class LatestModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache,
    ILogger<LatestModel> logger) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedRecentDiscussionList? LatestDiscussions { get; set; }
    public string? NextCursor { get; set; }

    public string SidebarScopeType { get; set; } = "platform";
    public string SidebarScopeId { get; set; } = "global";

    public bool ShowCommunityInDiscussionList =>
        CommunityContext.IsMultiCommunityEnabled
        && string.IsNullOrEmpty(CommunityContext.CommunitySlug)
        && !CommunityContext.IsCustomDomain;

    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }
    public SidebarLatestSpacesVM? InlineLatestSpaces { get; set; }
    public SidebarLatestContributorsVM? InlineLatestContributors { get; set; }

    public async Task OnGetAsync(int offset = 0, string? cursor = null)
    {
        string? communityId = null;
        if (CommunityContext.IsCustomDomain && !string.IsNullOrEmpty(CommunityContext.CommunitySlug))
        {
            var community = await _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug);
            communityId = community?.PublicId;
        }

        if (!string.IsNullOrEmpty(communityId))
        {
            SidebarScopeType = "community";
            SidebarScopeId = communityId;
        }

        ResolveSidebarData(communityId);
        await EnsureSidebarDataAsync(communityId);

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);
        try
        {
            var result = await _apiClient.GetNewDiscussionsAsync(offset, 20, communityId, cursor, viewerAllowsAdult: viewerAllowsAdult);
            LatestDiscussions = result;
            NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
        }
        catch { }
    }

    private async Task EnsureSidebarDataAsync(string? communityId)
    {
        var tasks = new List<Task>();
        if (InlinePlatformStats is null) tasks.Add(FetchPlatformStatsAsync(communityId));
        if (InlineLatestSpaces is null) tasks.Add(FetchSpacesAsync(communityId));
        if (InlineLatestContributors is null) tasks.Add(FetchContributorsAsync(communityId));
        await Task.WhenAll(tasks);
    }

    private async Task FetchPlatformStatsAsync(string? communityId)
    {
        try
        {
            if (!string.IsNullOrEmpty(communityId))
            {
                var data = await _apiClient.GetCommunityStatsAsync(communityId);
                if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "eager");
            }
            else
            {
                var data = await _apiClient.GetPlatformStatsAsync();
                if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "eager");
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch platform stats"); }
    }

    private async Task FetchSpacesAsync(string? communityId)
    {
        try
        {
            var data = await _apiClient.GetLatestActiveSpacesAsync(communityId: communityId);
            if (data is not null) InlineLatestSpaces = new(data, CommunityContext, "eager");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch latest spaces"); }
    }

    private async Task FetchContributorsAsync(string? communityId)
    {
        try
        {
            var data = await _apiClient.GetLatestContributorsAsync(communityId: communityId);
            if (data is not null) InlineLatestContributors = new(data, CommunityContext, "eager");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch latest contributors"); }
    }

    private void ResolveSidebarData(string? communityId)
    {
        if (!string.IsNullOrEmpty(communityId))
        {
            var data = prefetchCache.ResolveOrPrefetch(
                $"platform-stats:community:{communityId}",
                () => _apiClient.GetCommunityStatsAsync(communityId));
            if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }
        else
        {
            var data = prefetchCache.ResolveOrPrefetch(
                "platform-stats:platform:global",
                () => _apiClient.GetPlatformStatsAsync());
            if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }

        InlineLatestSpaces = prefetchCache.ResolveOrPrefetch(
            $"latest-spaces:{SidebarScopeType}:{SidebarScopeId}",
            () => _apiClient.GetLatestActiveSpacesAsync(communityId: communityId),
            d => new SidebarLatestSpacesVM(d, CommunityContext, "cache"));

        InlineLatestContributors = prefetchCache.ResolveOrPrefetch(
            $"latest-contributors:{SidebarScopeType}:{SidebarScopeId}",
            () => _apiClient.GetLatestContributorsAsync(communityId: communityId),
            d => new SidebarLatestContributorsVM(d, CommunityContext, "cache"));
    }
}
