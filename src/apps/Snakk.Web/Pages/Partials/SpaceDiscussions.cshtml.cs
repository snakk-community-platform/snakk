using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

[OutputCache(PolicyName = "AnonymousPartial")]
public class SpaceDiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PageModel
{
    public IEnumerable<DiscussionBySpaceInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }
    public int MaxOffset { get; set; }
    public string? NextCursor { get; set; }
    public ICommunityContext Community => communityContext;
    public string SpaceId { get; set; } = string.Empty;
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public int? TypeFilter { get; set; }

    public async Task OnGetAsync(string spaceId, int offset = 0, int pageSize = 20, int? typeFilter = null, string? cursor = null)
    {
        Response.Headers.CacheControl = "public, max-age=5";

        SpaceId = spaceId;
        TypeFilter = typeFilter;
        pageSize = Math.Clamp(pageSize, 1, 50);

        var maxPages = configuration.GetValue("EndlessScroll:MaxPages", 10);
        MaxOffset = maxPages * pageSize;

        if (offset >= MaxOffset)
        {
            Items = [];
            HasMoreItems = false;
            return;
        }

        // Look up space to get hub/space slugs for URL generation
        var space = await apiClient.GetSpaceAsync(spaceId);
        HubSlug = space?.HubSlug ?? "";
        SpaceSlug = space?.Slug ?? "";

        try
        {
            var result = await apiClient.GetDiscussionsBySpaceAsync(spaceId, offset, pageSize, typeFilter, cursor);
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
