namespace Snakk.Application.DTOs.Admin;

public class AdminDiscussionDto
{
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public required string AuthorDisplayName { get; set; }
    public required string SpaceSlug { get; set; }
    public required string SpaceName { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public int PostCount { get; set; }
    public required DateTime CreatedAt { get; set; }
}
