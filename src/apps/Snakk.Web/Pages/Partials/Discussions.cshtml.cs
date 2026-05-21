using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

[OutputCache(PolicyName = "AnonymousPartial")]
public class DiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IFollowedSpacesCacheService followedSpacesCache) : PageModel
{
    public IEnumerable<RecentDiscussionInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int Offset { get; set; }
    public int NextOffset { get; set; }
    public int MaxOffset { get; set; }
    public string? NextCursor { get; set; }
    public bool ShowCommunity { get; set; }
    public bool ShowHub { get; set; } = true;
    public bool ShowSpace { get; set; } = true;
    public ICommunityContext Community => communityContext;
    public string? CommunityId { get; set; }
    public string? HubId { get; set; }
    public string? SpaceId { get; set; }
    public bool HideCommunity { get; set; }
    public bool HideHub { get; set; }
    public bool HidePath { get; set; }
    public bool ShowPath { get; set; } = true;


    public string Sort { get; set; } = "recent";

    public async Task OnGetAsync(
        int offset = 0,
        int pageSize = 20,
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null,
        bool hideCommunity = false,
        bool hideHub = false,
        bool hidePath = false,
        string? cursor = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers.CacheControl = "public, max-age=5";

        Sort = sort == "trending" ? "trending" : sort == "new" ? "new" : sort == "my-feed" ? "my-feed" : "recent";
        CommunityId = communityId;
        HubId = hubId;
        SpaceId = spaceId;
        HideCommunity = hideCommunity;
        HideHub = hideHub;
        HidePath = hidePath;
        ShowPath = !hidePath;
        pageSize = Math.Clamp(pageSize, 1, 50);
        Offset = offset;

        var maxPages = configuration.GetValue("EndlessScroll:MaxPages", 10);
        MaxOffset = maxPages * pageSize;

        if (offset >= MaxOffset)
        {
            Items = [];
            HasMoreItems = false;
            return;
        }

        ShowCommunity = !hideCommunity
            && communityContext.IsMultiCommunityEnabled
            && string.IsNullOrEmpty(communityContext.CommunitySlug)
            && !communityContext.IsCustomDomain;
        ShowHub = !hideHub;

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, apiClient);

        try
        {
            if (Sort == "trending")
            {
                var result = await apiClient.GetTrendingDiscussionsAsync(offset, pageSize, communityId, viewerAllowsAdult: viewerAllowsAdult);
                Items = result?.Items ?? [];
                HasMoreItems = result?.HasMoreItems ?? false;
                NextOffset = offset + pageSize;
                NextCursor = null; // trending uses offset pagination
            }
            else if (Sort == "new")
            {
                var result = await apiClient.GetNewDiscussionsAsync(offset, pageSize, communityId, cursor, viewerAllowsAdult: viewerAllowsAdult);
                Items = result?.Items ?? [];
                HasMoreItems = result?.HasMoreItems ?? false;
                NextOffset = offset + pageSize;
                NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
            }
            else if (Sort == "my-feed")
            {
                Response.Headers.CacheControl = "private, no-store";
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId is not null)
                {
                    var spaceIds = await followedSpacesCache.GetAsync(userId, ct => apiClient.GetFollowedSpacesAsync(ct), cancellationToken);
                    if (spaceIds.Count > 0)
                    {
                        var result = await apiClient.GetRecentDiscussionsAsync(offset, pageSize, cursor: cursor, spaceIds: spaceIds, viewerAllowsAdult: viewerAllowsAdult);
                        Items = result?.Items ?? [];
                        HasMoreItems = result?.HasMoreItems ?? false;
                        NextOffset = offset + pageSize;
                        NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
                    }
                }
            }
            else
            {
                var result = await apiClient.GetRecentDiscussionsAsync(offset, pageSize, communityId, hubId, spaceId: spaceId, cursor: cursor, viewerAllowsAdult: viewerAllowsAdult);
                Items = result?.Items ?? [];
                HasMoreItems = result?.HasMoreItems ?? false;
                NextOffset = offset + pageSize;
                NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
            }
        }
        catch
        {
            // Return empty on failure
        }
    }
}
