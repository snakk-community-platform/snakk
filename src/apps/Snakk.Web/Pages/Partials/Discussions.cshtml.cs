using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class DiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PageModel
{
    public IEnumerable<RecentDiscussionInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }
    public bool ShowCommunity { get; set; }
    public ICommunityContext Community => communityContext;
    public string? CommunityId { get; set; }

    public async Task OnGetAsync(int offset = 0, int pageSize = 20, string? communityId = null)
    {
        Response.Headers.CacheControl = "public, max-age=5";

        CommunityId = communityId;
        pageSize = Math.Clamp(pageSize, 1, 50);

        ShowCommunity = configuration.GetValue<bool>("Features:MultiCommunityEnabled")
            && communityContext.IsDefaultCommunity
            && !communityContext.IsCustomDomain;

        try
        {
            var result = await apiClient.GetRecentDiscussionsAsync(offset, pageSize, communityId);
            Items = result?.Items ?? [];
            HasMoreItems = result?.HasMoreItems ?? false;
            NextOffset = offset + pageSize;
        }
        catch
        {
            // Return empty on failure
        }
    }
}
