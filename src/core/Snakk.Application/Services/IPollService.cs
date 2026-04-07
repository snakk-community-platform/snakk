namespace Snakk.Application.Services;

public record PollData(
    List<PollOptionData> Options,
    bool AllowMultipleChoices,
    bool AllowChangeVote,
    DateTime? ClosesAt,
    bool IsClosed,
    bool IsSecret,
    int TotalVotes,
    List<int> UserVotedOptionIds);

public record PollOptionData(int Id, string Text, int VoteCount, int DisplayOrder);

public interface IPollService
{
    Task<PollData?> GetPollAsync(string discussionPublicId, string? userPublicId = null);
    Task<(bool Success, string? Error)> VoteAsync(string discussionPublicId, int optionId, string userPublicId);
    Task<(bool Success, string? Error)> RemoveVoteAsync(string discussionPublicId, int optionId, string userPublicId);
}
