using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewGalleryModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : NewDiscussionBaseModel(apiClient, configuration, communityContext)
{
    protected override int DiscussionType => 5;
    protected override string TypeSlug => "gallery";
}
