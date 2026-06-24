using Microsoft.AspNetCore.OutputCaching;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

[OutputCache(PolicyName = "AnonymousPartial")]
public class SpaceDiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PaginatedPartialModel
{
    public IEnumerable<DiscussionBySpaceInfo> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public ICommunityContext Community => communityContext;
    public string SpaceId { get; set; } = string.Empty;
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public int? TypeFilter { get; set; }
    public bool IsModerator { get; set; }

    public async Task OnGetAsync(string spaceId, int offset = 0, int pageSize = 20, int? typeFilter = null, string? cursor = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

        // Mods see soft-deleted discussions; also bypass browser cache for their requests
        if (isAuthenticated)
        {
            IsModerator = await apiClient.CanModerateAsync(spaceId: spaceId, ct: cancellationToken);
            Response.Headers.CacheControl = IsModerator ? "private, no-store" : "public, max-age=5";
        }
        else
        {
            Response.Headers.CacheControl = "public, max-age=5";
        }

        SpaceId = spaceId;
        TypeFilter = typeFilter;
        (pageSize, var exceeded) = ApplyPaginationGuard(offset, pageSize, 1, 50, configuration);
        if (exceeded) return;

        // Look up space to get hub/space slugs for URL generation
        var space = await apiClient.GetSpaceAsync(spaceId);
        HubSlug = space?.HubSlug ?? "";
        SpaceSlug = space?.Slug ?? "";

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, apiClient);
        try
        {
            var result = await apiClient.GetDiscussionsBySpaceAsync(spaceId, offset, pageSize, typeFilter, cursor, viewerAllowsAdult: viewerAllowsAdult, includeDeleted: IsModerator);
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
