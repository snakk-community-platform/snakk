namespace Snakk.Application.DTOs.Admin;

public class AdminCommunityDetailDto
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public required string Visibility { get; init; }
    public DateTime CreatedAt { get; init; }
    public int HubCount { get; init; }
    public int MemberCount { get; init; }
    public required IReadOnlyList<AdminHubSummaryDto> Hubs { get; init; }
}

public class AdminHubSummaryDto
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public int SpaceCount { get; init; }
}
