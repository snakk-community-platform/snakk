namespace Snakk.Application.DTOs.Admin;

public class AdminSpaceDto
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string HubSlug { get; set; }
    public required string HubName { get; set; }
    public required string CommunitySlug { get; set; }
    public int DiscussionCount { get; set; }
    public required DateTime CreatedAt { get; set; }
}
