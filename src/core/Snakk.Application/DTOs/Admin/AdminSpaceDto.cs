namespace Snakk.Application.DTOs.Admin;

public record AdminSpaceDto : AdminScopeBaseDto
{
    public required string HubSlug { get; init; }
    public required string HubName { get; init; }
    public required string CommunitySlug { get; init; }
    public int DiscussionCount { get; init; }
}
