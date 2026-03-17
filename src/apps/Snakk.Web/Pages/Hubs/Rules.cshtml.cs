using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Hubs;

public class RulesModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public HubRulesResponse? Rules { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var hubResult = await apiClient.GetHubBySlugResultAsync(slug, CommunityContext.CommunitySlug!);

        if (!hubResult.IsSuccess)
            return hubResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Hub = hubResult.Value!;
        Rules = await apiClient.GetHubRulesAsync(Hub.PublicId);

        if (!string.IsNullOrEmpty(CommunityContext.CommunitySlug))
            CommunityDetail = await apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug);

        return Page();
    }
}
