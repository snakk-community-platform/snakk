namespace Snakk.Application.DTOs.Management;

public class HubSpacesDto
{
    public List<HubSpaceItemDto> Spaces { get; set; } = new();
    public int Total { get; set; }
}

public class HubSpaceItemDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsActive { get; set; }
}
