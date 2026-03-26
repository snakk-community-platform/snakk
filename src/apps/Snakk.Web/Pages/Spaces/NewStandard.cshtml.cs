using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewStandardModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : NewDiscussionBaseModel(apiClient, configuration, communityContext)
{
    protected override int DiscussionType => 0;
    protected override string TypeSlug => "standard";
}
