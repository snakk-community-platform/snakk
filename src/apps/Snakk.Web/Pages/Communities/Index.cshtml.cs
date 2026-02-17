using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Services;
using Snakk.Web.Models;

namespace Snakk.Web.Pages.Communities;

[OutputCache(PolicyName = "CommunitiesList")]
public class IndexModel(SnakkApiClient apiClient, IConfiguration configuration) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private readonly IConfiguration _configuration = configuration;

    public PagedResult<CommunityDto>? Communities { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0)
    {
        var multiCommunityEnabled = _configuration.GetValue<bool>("Features:MultiCommunityEnabled");
        if (!multiCommunityEnabled)
        {
            return RedirectToPage("/Index");
        }

        Communities = await _apiClient.GetCommunitiesAsync(offset, 20);
        return Page();
    }
}
