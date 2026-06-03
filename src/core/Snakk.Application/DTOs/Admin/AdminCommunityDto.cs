namespace Snakk.Application.DTOs.Admin;

public record AdminCommunityDto : AdminScopeBaseDto
{
    public int MemberCount { get; init; }
    public int HubCount { get; init; }
}
