namespace Snakk.Application.DTOs.Admin;

public record AdminHubDto : AdminScopeBaseDto
{
    public required string CommunitySlug { get; init; }
    public required string CommunityName { get; init; }
    public int SpaceCount { get; init; }
}
