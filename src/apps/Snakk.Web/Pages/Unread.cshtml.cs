namespace Snakk.Web.Pages;

using Snakk.Protos.Discussion;
using Snakk.Web.Services;

public class UnreadModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    ILogger<UnreadModel> logger) : BasePageModel(configuration, communityContext)
{
    public bool IsAuthenticated { get; set; }
    public PagedRecentDiscussionList? Discussions { get; set; }
    public string? NextCursor { get; set; }

    public bool ShowCommunityInDiscussionList =>
        CommunityContext.IsMultiCommunityEnabled
        && string.IsNullOrEmpty(CommunityContext.CommunitySlug)
        && !CommunityContext.IsCustomDomain;

    public async Task OnGetAsync(string? cursor = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        if (!IsAuthenticated) return;

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, apiClient);
        try
        {
            var result = await apiClient.GetRecentDiscussionsAsync(pageSize: 20, cursor: cursor, viewerAllowsAdult: viewerAllowsAdult, sinceLastVisit: true);
            Discussions = result;
            NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to load new-since-last-visit discussions"); }
    }
}
