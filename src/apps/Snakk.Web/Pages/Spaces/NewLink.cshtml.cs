using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewLinkModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    DiscussionCreateRateLimiter rateLimiter) : NewDiscussionBaseModel(apiClient, configuration, communityContext, rateLimiter)
{
    protected override int DiscussionType => 4;
    protected override string TypeSlug => "link";

    [BindProperty] public string? LinkUrl { get; set; }

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
        => await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            NewContent!,
            DiscussionType,
            linkUrl: LinkUrl);
}
