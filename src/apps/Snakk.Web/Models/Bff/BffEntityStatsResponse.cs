namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF entity stats responses - used for hover popups (hubs, spaces, communities)
/// </summary>
public record BffHubStatsResponse
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public required int SpaceCount { get; init; }
    public required int DiscussionCount { get; init; }
    public required int ReplyCount { get; init; }
    public string? GradientCss { get; init; }
}

public record BffSpaceStatsResponse
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public required int DiscussionCount { get; init; }
    public required int ReplyCount { get; init; }
    public required int FollowerCount { get; init; }
    public string? GradientCss { get; init; }
}

public record BffCommunityStatsResponse
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public required int HubCount { get; init; }
    public required int SpaceCount { get; init; }
    public required int DiscussionCount { get; init; }
    public required int ReplyCount { get; init; }
    public string? GradientCss { get; init; }
}
