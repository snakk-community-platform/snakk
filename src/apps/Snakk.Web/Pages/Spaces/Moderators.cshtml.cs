using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Moderation;
using Snakk.Protos.Space;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class ModeratorsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public SpaceInfo? Space { get; set; }
    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public GetModeratorsResponse? Moderators { get; set; }
    public string HubSlug { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string hubSlug, string slug)
    {
        HubSlug = hubSlug;

        var hubTask = apiClient.GetHubBySlugResultAsync(hubSlug, CommunityContext.CommunitySlug!);
        var spaceTask = apiClient.GetSpaceBySlugResultAsync(slug, hubSlug);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, spaceTask, communityTask);

        if (!hubTask.Result.IsSuccess)
            return hubTask.Result.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        if (!spaceTask.Result.IsSuccess)
            return spaceTask.Result.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Hub = hubTask.Result.Value!;
        Space = spaceTask.Result.Value!;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        Moderators = await apiClient.GetModeratorsAsync("Space", Space.PublicId);

        return Page();
    }
}
