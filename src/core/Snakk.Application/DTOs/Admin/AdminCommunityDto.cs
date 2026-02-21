namespace Snakk.Application.DTOs.Admin;

public class AdminCommunityDto
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int MemberCount { get; set; }
    public int HubCount { get; set; }
    public required DateTime CreatedAt { get; set; }
}
