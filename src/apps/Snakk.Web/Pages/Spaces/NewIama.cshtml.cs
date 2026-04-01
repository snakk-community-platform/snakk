using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

public class NewIamaModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : NewDiscussionBaseModel(apiClient, configuration, communityContext)
{
    protected override int DiscussionType => 9;
    protected override string TypeSlug => "iama";

    [BindProperty] public bool IamaIsScheduled { get; set; }
    [BindProperty] public string? IamaScheduledStart { get; set; }
    [BindProperty] public string? IamaScheduledEnd { get; set; }
    [BindProperty] public string? IamaVerificationNote { get; set; }

    protected override async Task<Snakk.Protos.Discussion.DiscussionCreatedInfo?> CreateDiscussionAsync()
    {
        DateTime? startDate = null;
        if (IamaIsScheduled && !string.IsNullOrWhiteSpace(IamaScheduledStart) && DateTime.TryParse(IamaScheduledStart, out var parsedStart))
            startDate = DateTime.SpecifyKind(parsedStart, DateTimeKind.Utc);

        DateTime? endDate = null;
        if (!string.IsNullOrWhiteSpace(IamaScheduledEnd) && DateTime.TryParse(IamaScheduledEnd, out var parsedEnd))
            endDate = DateTime.SpecifyKind(parsedEnd, DateTimeKind.Utc);

        return await ApiClient.CreateDiscussionAsync(
            Space!.PublicId,
            NewTitle!.Trim(),
            NewContent!,
            DiscussionType,
            iamaIsScheduled: IamaIsScheduled,
            iamaScheduledStart: startDate,
            iamaScheduledEnd: endDate,
            iamaVerificationNote: string.IsNullOrWhiteSpace(IamaVerificationNote) ? null : IamaVerificationNote.Trim());
    }
}
