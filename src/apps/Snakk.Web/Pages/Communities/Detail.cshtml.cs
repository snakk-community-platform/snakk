using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;

namespace Snakk.Web.Pages.Communities;

public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private readonly IConfiguration _configuration = configuration;

    public CommunityInfo? Community { get; set; }
    public PagedHubList? Hubs { get; set; }
    public ICommunityContext CommunityContext => communityContext;
    public SidebarCommunityRulesVM? InlineCommunityRules { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0)
    {
        var multiCommunityEnabled = _configuration.GetValue<bool>("Features:MultiCommunityEnabled");
        if (!multiCommunityEnabled)
        {
            return RedirectToPage("/Index");
        }

        Community = await _apiClient.GetCommunityBySlugAsync(slug);

        if (Community is null)
        {
            return NotFound();
        }

        if (Community.HasRules)
            InlineCommunityRules = prefetchCache.ResolveOrPrefetch(
                $"community-rules:{Community.PublicId}",
                () => _apiClient.GetCommunityRulesAsync(Community.PublicId),
                d => new SidebarCommunityRulesVM(d, "cache"));

        Hubs = await _apiClient.GetHubsByCommunityAsync(Community.PublicId, offset, 20);

        return Page();
    }
}
