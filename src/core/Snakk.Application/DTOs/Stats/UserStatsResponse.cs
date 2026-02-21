namespace Snakk.Application.DTOs.Stats;

public record UserStatsResponse
{
    public required string PublicId { get; init; }
    public required string DisplayName { get; init; }
    public required string AvatarUrl { get; init; }
    public required int DiscussionCount { get; init; }
    public required int ReplyCount { get; init; }
    public required int FollowerCount { get; init; }
    public required int FollowingCount { get; init; }
}
