namespace Snakk.Application.DTOs.Management;

public class SpaceOverviewDto
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CommunitySlug { get; set; } = string.Empty;
    public string CommunityName { get; set; } = string.Empty;
    public string HubSlug { get; set; } = string.Empty;
    public string HubName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Stats
    public int TotalDiscussions { get; set; }
    public int TotalPosts { get; set; }
    public int ActiveMembers { get; set; }
    public int Followers { get; set; }

    // Activity
    public int PostsToday { get; set; }
    public int PostsThisWeek { get; set; }
    public int NewDiscussionsToday { get; set; }
    public int NewDiscussionsThisWeek { get; set; }

    // Moderation
    public int PendingReports { get; set; }

    // Team
    public List<ScopeModeratorDto> Moderators { get; set; } = new();

    // Recent Activity
    public List<RecentActivityItemDto> RecentActivity { get; set; } = new();
}
