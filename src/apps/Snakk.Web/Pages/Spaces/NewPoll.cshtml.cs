using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewPollModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    DiscussionCreateRateLimiter rateLimiter) : NewDiscussionBaseModel(apiClient, configuration, communityContext, rateLimiter)
{
    protected override int DiscussionType => 2;
    protected override string TypeSlug => "poll";

    [BindProperty] public List<string> PollOptions { get; set; } = [];
    [BindProperty] public bool PollAllowMultiple { get; set; }
    [BindProperty] public bool PollAllowChangeVote { get; set; }
    [BindProperty] public string? PollClosesAt { get; set; }
    [BindProperty] public bool PollSecret { get; set; }
    [BindProperty] public bool PollIsSegmented { get; set; }
    [BindProperty] public string? PollSegmentLabel { get; set; }
    [BindProperty] public string? PollSegmentOptionA { get; set; }
    [BindProperty] public string? PollSegmentOptionB { get; set; }

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
    {
        var options = PollOptions?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList() ?? [];

        DateTime? closeDate = null;
        if (!string.IsNullOrWhiteSpace(PollClosesAt) && DateTime.TryParse(PollClosesAt, out var parsed))
            closeDate = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

        return await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            NewContent!,
            DiscussionType,
            pollOptions: options,
            pollAllowMultiple: PollAllowMultiple,
            pollAllowChangeVote: PollAllowChangeVote,
            pollClosesAt: closeDate,
            pollSecret: PollSecret,
            pollIsSegmented: PollIsSegmented,
            pollSegmentLabel: PollSegmentLabel,
            pollSegmentOptionA: PollSegmentOptionA,
            pollSegmentOptionB: PollSegmentOptionB);
    }
}
