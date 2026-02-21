namespace Snakk.Application.DTOs.Stats;

public record SpaceStatsResponse
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string AvatarUrl { get; init; }
    public required int DiscussionCount { get; init; }
    public required int ReplyCount { get; init; }
    public required int FollowerCount { get; init; }
}
