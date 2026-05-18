namespace Snakk.Application.Services;

public record PollData(
    List<PollOptionData> Options,
    bool AllowMultipleChoices,
    bool AllowChangeVote,
    DateTime? ClosesAt,
    bool IsClosed,
    bool IsSecret,
    int TotalVotes,
    List<int> UserVotedOptionIds,
    bool IsSegmented = false,
    string? SegmentLabel = null,
    string? SegmentOptionA = null,
    string? SegmentOptionB = null,
    List<PollOptionSegmentData>? SegmentVotes = null,
    int? UserSegmentIndex = null);

public record PollOptionSegmentData(int OptionId, int SegmentACount, int SegmentBCount);

public record PollOptionData(int Id, string Text, int VoteCount, int DisplayOrder);

public interface IPollService
{
    Task<PollData?> GetPollAsync(string discussionPublicId, string? userPublicId = null, CancellationToken ct = default);
    Task<(bool Success, string? Error)> VoteAsync(string discussionPublicId, int optionId, string userPublicId, int? segmentIndex = null, CancellationToken ct = default);
    Task<(bool Success, string? Error)> RemoveVoteAsync(string discussionPublicId, int optionId, string userPublicId, CancellationToken ct = default);
}
