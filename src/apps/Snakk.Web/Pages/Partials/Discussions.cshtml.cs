using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

[OutputCache(PolicyName = "AnonymousPartial")]
public class DiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PageModel
{
    public IEnumerable<RecentDiscussionInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }
    public int MaxOffset { get; set; }
    public string? NextCursor { get; set; }
    public bool ShowCommunity { get; set; }
    public bool ShowHub { get; set; } = true;
    public bool ShowSpace { get; set; } = true;
    public ICommunityContext Community => communityContext;
    public string? CommunityId { get; set; }
    public string? HubId { get; set; }
    public bool HideCommunity { get; set; }
    public bool HideHub { get; set; }

    public async Task OnGetAsync(
        int offset = 0,
        int pageSize = 20,
        string? communityId = null,
        string? hubId = null,
        bool hideCommunity = false,
        bool hideHub = false,
        string? cursor = null)
    {
        Response.Headers.CacheControl = "public, max-age=5";

        CommunityId = communityId;
        HubId = hubId;
        HideCommunity = hideCommunity;
        HideHub = hideHub;
        pageSize = Math.Clamp(pageSize, 1, 50);

        var maxPages = configuration.GetValue("EndlessScroll:MaxPages", 20);
        MaxOffset = maxPages * pageSize;

        ShowCommunity = !hideCommunity
            && communityContext.IsMultiCommunityEnabled
            && string.IsNullOrEmpty(communityContext.CommunitySlug)
            && !communityContext.IsCustomDomain;
        ShowHub = !hideHub;

        try
        {
            var result = await apiClient.GetRecentDiscussionsAsync(offset, pageSize, communityId, hubId, cursor);
            Items = result?.Items ?? [];
            HasMoreItems = result?.HasMoreItems ?? false;
            NextOffset = offset + pageSize;
            NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
        }
        catch
        {
            // Return empty on failure
        }
    }
}
