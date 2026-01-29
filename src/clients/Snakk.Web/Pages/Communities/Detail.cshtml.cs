using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;
using Snakk.Web.Models;

namespace Snakk.Web.Pages.Communities;

public class DetailModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private readonly IConfiguration _configuration = configuration;

    public CommunityDetailDto? Community { get; set; }
    public PagedResult<HubDto>? Hubs { get; set; }
    public ICommunityContext CommunityContext => communityContext;

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0)
    {
        var multiCommunityEnabled = _configuration.GetValue<bool>("Features:MultiCommunityEnabled");
        if (!multiCommunityEnabled)
        {
            return RedirectToPage("/Index");
        }

        Community = await _apiClient.GetCommunityBySlugAsync(slug);

        if (Community == null)
        {
            return NotFound();
        }

        Hubs = await _apiClient.GetHubsByCommunityAsync(Community.PublicId, offset, 20);

        return Page();
    }
}
