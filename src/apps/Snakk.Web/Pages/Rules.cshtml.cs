using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Community;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class RulesModel(SnakkApiClient apiClient) : PageModel
{
    public SiteRulesResponse? Rules { get; set; }

    public async Task OnGetAsync()
    {
        Rules = await apiClient.GetSiteRulesAsync();
    }
}
