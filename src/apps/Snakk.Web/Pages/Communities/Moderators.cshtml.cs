using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Community;
using Snakk.Protos.Moderation;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Communities;

public class ModeratorsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public CommunityInfo? CommunityDetail { get; set; }
    public GetModeratorsResponse? Moderators { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var multiCommunityEnabled = Configuration.GetValue<bool>("Features:MultiCommunityEnabled");
        if (!multiCommunityEnabled)
            return RedirectToPage("/Moderators");

        var communityResult = await apiClient.GetCommunityBySlugResultAsync(slug);

        if (!communityResult.IsSuccess)
            return communityResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        CommunityDetail = communityResult.Value!;
        Moderators = await apiClient.GetModeratorsAsync("Community", CommunityDetail.PublicId);

        return Page();
    }
}
