namespace Snakk.Application.Services;

public interface IDiscussionExtensionService
{
    Task CreateQuestionAsync(string discussionPublicId, CancellationToken ct = default);
    Task CreateGuideAsync(string discussionPublicId, CancellationToken ct = default);
    Task CreateImagesAsync(string discussionPublicId, string layout = "grid", List<string>? imageUrls = null, bool isSpoiler = false, CancellationToken ct = default);

    Task CreatePollAsync(
        string discussionPublicId,
        List<string> options,
        bool allowMultipleChoices = false,
        bool allowChangeVote = false,
        DateTime? closesAt = null,
        bool votesVisible = true,
        bool isSegmented = false,
        string? segmentLabel = null,
        string? segmentOptionA = null,
        string? segmentOptionB = null,
        CancellationToken ct = default);

    Task CreateLinkAsync(
        string discussionPublicId,
        string url,
        string? title = null,
        string? description = null,
        string? imageUrl = null,
        string? domain = null,
        CancellationToken ct = default);

    Task CreateDebateAsync(
        string discussionPublicId,
        List<string> positionLabels,
        bool allowNeutral = false,
        CancellationToken ct = default);

    Task CreateJournalAsync(string discussionPublicId, CancellationToken ct = default);
    Task AddJournalEntryAsync(string discussionPublicId, string postPublicId, CancellationToken ct = default);
    Task MarkQuestionSolvedAsync(string discussionPublicId, string acceptedPostPublicId, CancellationToken ct = default);

    Task CreateIamaAsync(
        string discussionPublicId,
        bool isScheduled = false,
        DateTime? scheduledStartUtc = null,
        DateTime? scheduledEndUtc = null,
        string? verificationNote = null,
        CancellationToken ct = default);
}
