using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Community;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class RulesModel(SnakkApiClient apiClient) : PageModel
{
    public SiteRulesResponse? Rules { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Rules = await apiClient.GetSiteRulesAsync();
    }
}
