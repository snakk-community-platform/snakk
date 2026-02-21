namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF user stats response - optimized for frontend display
/// </summary>
public record BffUserStatsResponse
{
    public required string PublicId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required int DiscussionCount { get; init; }
    public required int ReplyCount { get; init; }
    public required int FollowerCount { get; init; }
    public required int FollowingCount { get; init; }
}
