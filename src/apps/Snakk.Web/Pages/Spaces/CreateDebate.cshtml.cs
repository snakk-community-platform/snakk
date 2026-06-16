using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewDebateModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    DiscussionCreateRateLimiter rateLimiter) : CreateDiscussionBaseModel(apiClient, configuration, communityContext, rateLimiter)
{
    protected override int DiscussionType => 7;
    protected override string TypeSlug => "debate";

    [BindProperty] public List<string> DebatePositions { get; set; } = [];
    [BindProperty] public bool DebateAllowNeutral { get; set; }

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
    {
        var positions = DebatePositions?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? [];

        return await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            NewContent!,
            DiscussionType,
            debatePositions: positions,
            debateAllowNeutral: DebateAllowNeutral);
    }
}
