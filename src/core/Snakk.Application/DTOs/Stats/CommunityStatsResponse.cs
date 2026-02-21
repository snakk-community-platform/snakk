namespace Snakk.Application.DTOs.Stats;

public record CommunityStatsResponse
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string AvatarUrl { get; init; }
    public required int HubCount { get; init; }
    public required int SpaceCount { get; init; }
    public required int DiscussionCount { get; init; }
    public required int ReplyCount { get; init; }
}
