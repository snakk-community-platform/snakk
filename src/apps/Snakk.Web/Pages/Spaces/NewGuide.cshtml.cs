using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewGuideModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : NewDiscussionBaseModel(apiClient, configuration, communityContext)
{
    protected override int DiscussionType => 6;
    protected override string TypeSlug => "guide";
}
