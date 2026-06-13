namespace Snakk.Application.DTOs.Admin;

public class ContentOverviewExtendedDto
{
    public int TotalCommunities { get; init; }
    public int TotalHubs { get; init; }
    public int TotalSpaces { get; init; }
    public int TotalDiscussions { get; init; }
    public int TotalPosts { get; init; }
    public int PinnedDiscussions { get; init; }
    public int LockedDiscussions { get; init; }
}
