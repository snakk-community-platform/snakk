using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewJournalModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    DiscussionCreateRateLimiter rateLimiter) : CreateDiscussionBaseModel(apiClient, configuration, communityContext, rateLimiter)
{
    protected override int DiscussionType => 8;
    protected override string TypeSlug => "journal";
}
