using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Services;
using Snakk.Protos.Discussion;

namespace Snakk.Web.Pages;

[OutputCache(PolicyName = "AnonymousPage")]
public class LatestModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
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

    public async Task OnGetAsync(int offset = 0, string? cursor = null, CancellationToken cancellationToken = default)
    {
        Preload("discussion-card");
        cancellationToken.ThrowIfCancellationRequested();
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

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);
        try
        {
            var result = await _apiClient.GetNewDiscussionsAsync(offset, 20, communityId, cursor, viewerAllowsAdult: viewerAllowsAdult);
            LatestDiscussions = result;
            NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
        }
        catch { }
    }
}
