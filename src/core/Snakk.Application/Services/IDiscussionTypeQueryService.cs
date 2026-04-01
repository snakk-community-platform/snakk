namespace Snakk.Application.Services;

// Question
public record QuestionStatus(bool IsSolved, string? AcceptedPostPublicId, DateTime? SolvedAt);

// Debate
public record DebateInfo(List<DebatePositionData> Positions, bool AllowNeutral, Dictionary<string, int> PostPositions);
public record DebatePositionData(int Id, string Label, int Index, int PostCount);

// Link
public record LinkInfo(string Url, string? Title, string? Description, string? ImageUrl, string? Domain, string? OEmbedHtml, string? LocalImagePath, string? BlurDataUri, bool IsInternal);

// Gallery
public record GalleryImageInfo(string Url, string? ThumbnailUrl, string? BlurDataUri);

// Journal
public record JournalInfo(List<string> EntryPostPublicIds);

// IAMA
public record IamaInfo(
    int Phase,
    bool IsScheduled,
    DateTime? ScheduledStartUtc,
    DateTime? ScheduledEndUtc,
    string? VerificationNote,
    Dictionary<string, string> OfficialAnswers,
    List<string> BestQuestionPostPublicIds);

public interface IDiscussionTypeQueryService
{
    // Question
    Task<QuestionStatus?> GetQuestionStatusAsync(string discussionPublicId);
    Task<(bool Success, string? Error)> MarkQuestionSolvedAsync(string discussionPublicId, string postPublicId, string userPublicId);

    // Gallery
    Task<string?> GetGalleryLayoutAsync(string discussionPublicId);
    Task<List<GalleryImageInfo>> GetGalleryImagesAsync(string discussionPublicId);

    // Debate
    Task<DebateInfo?> GetDebateInfoAsync(string discussionPublicId);
    Task<(bool Success, string? Error)> SetPostDebatePositionAsync(string discussionPublicId, string postPublicId, int positionId, string userPublicId);

    // Link
    Task<LinkInfo?> GetLinkInfoAsync(string discussionPublicId);

    // Journal
    Task<JournalInfo?> GetJournalInfoAsync(string discussionPublicId);
    Task<(bool Success, string? Error)> AddJournalEntryAsync(string discussionPublicId, string postPublicId, string userPublicId);

    // IAMA
    Task<IamaInfo?> GetIamaInfoAsync(string discussionPublicId);
    Task<(bool Success, string? Error)> MarkIamaOfficialAnswerAsync(string discussionPublicId, string questionPostPublicId, string answerPostPublicId, string userPublicId);
    Task<(bool Success, string? Error)> SetIamaBestQuestionsAsync(string discussionPublicId, List<string> postPublicIds, string userPublicId);
    Task<(bool Success, string? Error)> TransitionIamaPhaseAsync(string discussionPublicId, int newPhase, string userPublicId);
}
