using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewGuideModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    DiscussionCreateRateLimiter rateLimiter) : CreateDiscussionBaseModel(apiClient, configuration, communityContext, rateLimiter)
{
    protected override int DiscussionType => 6;
    protected override string TypeSlug => "guide";
    protected override void PreloadPageCss() { base.PreloadPageCss(); Preload("type-guide"); }
}
