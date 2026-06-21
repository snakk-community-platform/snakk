using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewQuestionModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    DiscussionCreateRateLimiter rateLimiter) : CreateDiscussionBaseModel(apiClient, configuration, communityContext, rateLimiter)
{
    protected override int DiscussionType => 1;
    protected override string TypeSlug => "question";
    protected override void PreloadPageCss() { base.PreloadPageCss(); Preload("type-question"); }
}
