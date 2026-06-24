using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class SavedDiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PaginatedPartialModel
{
    public ICommunityContext Community => communityContext;
    public IList<RecentDiscussionInfo> Items { get; set; } = [];
    public bool ShowCommunity { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RequireAuthCookie() is { } r) return r;

        (pageSize, var exceeded) = ApplyPaginationGuard(offset, pageSize, 1, 50, configuration);
        if (exceeded) return Page();

        ShowCommunity = communityContext.ShouldShowCommunity();

        try
        {
            var result = await apiClient.GetSavedDiscussionsAsync(offset, pageSize);
            if (result != null)
            {
                Items = result.Items;
                HasMoreItems = result.HasMoreItems;
                NextOffset = offset + result.Items.Count;
            }
        }
        catch { }

        return Page();
    }
}
