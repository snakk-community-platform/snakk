using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Moderation;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class ModeratorsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext)
    : BasePageModel(configuration, communityContext)
{
    public GetModeratorsResponse? Moderators { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Preload("discussion-card");
        cancellationToken.ThrowIfCancellationRequested();
        Moderators = await apiClient.GetSiteModeratorsAsync();
    }
}
