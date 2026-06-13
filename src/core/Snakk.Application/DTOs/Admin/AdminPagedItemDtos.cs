namespace Snakk.Application.DTOs.Admin;

/// <summary>
/// Rich community item used by the admin REST endpoint (includes publicId and visibility).
/// Distinct from AdminCommunityDto which uses slug-based identifiers for the gRPC/manage paths.
/// </summary>
public class AdminCommunityItemDto
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public required string Visibility { get; init; }
    public DateTime CreatedAt { get; init; }
    public int HubCount { get; init; }
    public int MemberCount { get; init; }
}

/// <summary>
/// Rich hub item used by the admin REST endpoint (includes publicId, community publicId).
/// </summary>
public class AdminHubItemDto
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public required string CommunityPublicId { get; init; }
    public required string CommunityName { get; init; }
    public DateTime CreatedAt { get; init; }
    public int SpaceCount { get; init; }
}

/// <summary>
/// Rich space item used by the admin REST endpoint (includes publicIds).
/// </summary>
public class AdminSpaceItemDto
{
    public required string PublicId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public required string HubPublicId { get; init; }
    public required string HubName { get; init; }
    public required string CommunityPublicId { get; init; }
    public required string CommunityName { get; init; }
    public DateTime CreatedAt { get; init; }
    public int DiscussionCount { get; init; }
}

/// <summary>
/// Rich discussion item used by the admin REST endpoint (includes publicIds, author, pinned/locked).
/// </summary>
public class AdminDiscussionItemDto
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
    public bool IsPinned { get; init; }
    public bool IsLocked { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastActivityAt { get; init; }
}
