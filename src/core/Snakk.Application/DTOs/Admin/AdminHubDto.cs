namespace Snakk.Application.DTOs.Admin;

public class AdminHubDto
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string CommunitySlug { get; set; }
    public required string CommunityName { get; set; }
    public int SpaceCount { get; set; }
    public required DateTime CreatedAt { get; set; }
}
