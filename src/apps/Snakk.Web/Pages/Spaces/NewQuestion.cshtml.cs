using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewQuestionModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : NewDiscussionBaseModel(apiClient, configuration, communityContext)
{
    protected override int DiscussionType => 1;
    protected override string TypeSlug => "question";
}
