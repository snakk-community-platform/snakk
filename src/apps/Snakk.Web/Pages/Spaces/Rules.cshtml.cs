using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class RulesModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public SpaceInfo? Space { get; set; }
    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public SpaceRulesResponse? Rules { get; set; }
    public string HubSlug { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string hubSlug, string slug, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HubSlug = hubSlug;

        var hubTask = apiClient.GetHubBySlugResultAsync(hubSlug, CommunityContext.CommunitySlug!);
        var spaceTask = apiClient.GetSpaceBySlugResultAsync(slug, hubSlug);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, spaceTask, communityTask);

        if (!hubTask.Result.IsSuccess)
            return GrpcError(hubTask.Result);

        if (!spaceTask.Result.IsSuccess)
            return GrpcError(spaceTask.Result);

        Hub = hubTask.Result.Value!;
        Space = spaceTask.Result.Value!;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        Rules = await apiClient.GetSpaceRulesAsync(Space.PublicId);

        return Page();
    }
}
