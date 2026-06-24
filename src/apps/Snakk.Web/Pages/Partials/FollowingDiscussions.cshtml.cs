using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class FollowingDiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PaginatedPartialModel
{
    public IList<RecentDiscussionInfo> Items { get; set; } = [];
    public bool ShowCommunity { get; set; }
    public ICommunityContext Community => communityContext;
    public Dictionary<string, DateTime> FollowTimestamps { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RequireAuthCookie() is { } r) return r;

        (pageSize, var exceeded) = ApplyPaginationGuard(offset, pageSize, 1, 20, configuration);
        if (exceeded) return Page();

        ShowCommunity = communityContext.ShouldShowCommunity();

        try
        {
            var allDetails = await apiClient.GetFollowedDiscussionsDetailedAsync(cancellationToken);
            FollowTimestamps = allDetails.ToDictionary(x => x.PublicId, x => x.FollowedAt);
            var allIds = allDetails.Select(x => x.PublicId).ToList();
            var batch = allIds.Skip(offset).Take(pageSize).ToList();

            HasMoreItems = offset + pageSize < allIds.Count;
            NextOffset = offset + pageSize;

            if (batch.Count > 0)
            {
                var result = await apiClient.GetRecentDiscussionsByIdsAsync(batch);
                Items = result?.Items ?? [];
            }
        }
        catch { }

        return Page();
    }
}
