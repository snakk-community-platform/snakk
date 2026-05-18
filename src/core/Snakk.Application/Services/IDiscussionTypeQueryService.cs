namespace Snakk.Application.Services;

// Question
public record QuestionStatus(bool IsSolved, string? AcceptedPostPublicId, DateTime? SolvedAt);

// Debate
public record DebateInfo(List<DebatePositionData> Positions, bool AllowNeutral, Dictionary<string, int> PostPositions);
public record DebatePositionData(int Id, string Label, int Index, int PostCount);

// Link
public record LinkInfo(string Url, string? Title, string? Description, string? ImageUrl, string? Domain, string? OEmbedHtml, string? ImagePath, string? BlurDataUri, bool IsInternal, int? ImageWidth = null, int? ImageHeight = null);

// Images
public record ImagesImageInfo(
    string Url,
    string? ThumbnailUrl,
    string? MediumThumbnailUrl,
    string? BlurDataUri,
    int Width,
    int Height,
    int? ThumbnailWidth,
    int? ThumbnailHeight,
    int? MediumThumbnailWidth,
    int? MediumThumbnailHeight);

// Journal
public record JournalInfo(List<string> EntryPostPublicIds);

// IAMA
public record IamaInfo(
    int Phase,
    bool IsScheduled,
    DateTime? ScheduledStartUtc,
    DateTime? ScheduledEndUtc,
    DateTime? ActualStartedAtUtc,
    DateTime? ActualEndedAtUtc,
    string? VerificationNote,
    string? VerificationNoteHtml,
    Dictionary<string, string> OfficialAnswers,
    List<string> BestQuestionPostPublicIds);

public interface IDiscussionTypeQueryService
{
    // Question
    Task<QuestionStatus?> GetQuestionStatusAsync(string discussionPublicId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> MarkQuestionSolvedAsync(string discussionPublicId, string postPublicId, string userPublicId, CancellationToken ct = default);

    // Images
    Task<(string? Layout, bool IsSpoiler)> GetImagesInfoAsync(string discussionPublicId, CancellationToken ct = default);
    Task<List<ImagesImageInfo>> GetImagesListAsync(string discussionPublicId, CancellationToken ct = default);

    // Debate
    Task<DebateInfo?> GetDebateInfoAsync(string discussionPublicId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> SetPostDebatePositionAsync(string discussionPublicId, string postPublicId, int positionId, string userPublicId, CancellationToken ct = default);

    // Link
    Task<LinkInfo?> GetLinkInfoAsync(string discussionPublicId, CancellationToken ct = default);

    // Journal
    Task<JournalInfo?> GetJournalInfoAsync(string discussionPublicId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> AddJournalEntryAsync(string discussionPublicId, string postPublicId, string userPublicId, CancellationToken ct = default);

    // IAMA
    Task<IamaInfo?> GetIamaInfoAsync(string discussionPublicId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> MarkIamaOfficialAnswerAsync(string discussionPublicId, string questionPostPublicId, string answerPostPublicId, string userPublicId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> SetIamaBestQuestionsAsync(string discussionPublicId, List<string> postPublicIds, string userPublicId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> TransitionIamaPhaseAsync(string discussionPublicId, int newPhase, string userPublicId, CancellationToken ct = default);
}
