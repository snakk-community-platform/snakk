namespace Snakk.Application.Services;

public interface IDiscussionExtensionService
{
    Task CreateQuestionAsync(string discussionPublicId);
    Task CreateGuideAsync(string discussionPublicId);
    Task CreateGalleryAsync(string discussionPublicId, string layout = "grid", List<string>? imageUrls = null);

    Task CreatePollAsync(
        string discussionPublicId,
        List<string> options,
        bool allowMultipleChoices = false,
        bool allowChangeVote = false,
        DateTime? closesAt = null);

    Task CreateLinkAsync(
        string discussionPublicId,
        string url,
        string? title = null,
        string? description = null,
        string? imageUrl = null,
        string? domain = null);

    Task CreateDebateAsync(
        string discussionPublicId,
        List<string> positionLabels,
        bool allowNeutral = false);

    Task CreateJournalAsync(string discussionPublicId);
    Task AddJournalEntryAsync(string discussionPublicId, string postPublicId);
    Task MarkQuestionSolvedAsync(string discussionPublicId, string acceptedPostPublicId);
}
