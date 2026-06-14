namespace Snakk.Application.DTOs.Admin;

public class AdminDiscussionDetailDto
{
    public required string PublicId { get; init; }
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string SpacePublicId { get; init; }
    public required string SpaceName { get; init; }
    public required string HubPublicId { get; init; }
    public required string HubName { get; init; }
    public required string CommunityPublicId { get; init; }
    public required string CommunityName { get; init; }
    public required string AuthorPublicId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public int PostCount { get; init; }
    public int ReactionCount { get; init; }
    public bool IsPinned { get; init; }
    public bool IsLocked { get; init; }
    public string? Tags { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastActivityAt { get; init; }
}
