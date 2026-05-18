using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Moderation;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class ModeratorsModel(SnakkApiClient apiClient) : PageModel
{
    public GetModeratorsResponse? Moderators { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Moderators = await apiClient.GetSiteModeratorsAsync();
    }
}
