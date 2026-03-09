using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class SpaceDiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PageModel
{
    public IEnumerable<DiscussionBySpaceInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }
    public int MaxOffset { get; set; }
    public ICommunityContext Community => communityContext;
    public string SpaceId { get; set; } = string.Empty;
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public int? TypeFilter { get; set; }

    public async Task OnGetAsync(string spaceId, string hubSlug, string spaceSlug, int offset = 0, int pageSize = 20, int? typeFilter = null)
    {
        Response.Headers.CacheControl = "public, max-age=5";

        SpaceId = spaceId;
        HubSlug = hubSlug;
        SpaceSlug = spaceSlug;
        TypeFilter = typeFilter;
        pageSize = Math.Clamp(pageSize, 1, 50);

        var maxPages = configuration.GetValue("EndlessScroll:MaxPages", 20);
        MaxOffset = maxPages * pageSize;

        try
        {
            var result = await apiClient.GetDiscussionsBySpaceAsync(spaceId, offset, pageSize, typeFilter);
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
