using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Community;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Communities;

public class RulesModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public CommunityInfo? CommunityDetail { get; set; }
    public CommunityRulesResponse? Rules { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var multiCommunityEnabled = Configuration.GetValue<bool>("Features:MultiCommunityEnabled");
        if (!multiCommunityEnabled)
            return RedirectToPage("/Rules");

        var communityResult = await apiClient.GetCommunityBySlugResultAsync(slug);

        if (!communityResult.IsSuccess)
            return GrpcError(communityResult);

        CommunityDetail = communityResult.Value!;
        Rules = await apiClient.GetCommunityRulesAsync(CommunityDetail.PublicId);

        return Page();
    }
}
