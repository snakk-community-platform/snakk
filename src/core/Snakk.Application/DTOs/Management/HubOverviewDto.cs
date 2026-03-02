namespace Snakk.Application.DTOs.Management;

public class HubOverviewDto
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CommunitySlug { get; set; } = string.Empty;
    public string CommunityName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Stats
    public int TotalSpaces { get; set; }
    public int TotalDiscussions { get; set; }
    public int TotalPosts { get; set; }
    public int ActiveMembers { get; set; }

    // Activity
    public int PostsToday { get; set; }
    public int PostsThisWeek { get; set; }
    public int NewDiscussionsToday { get; set; }
    public int NewDiscussionsThisWeek { get; set; }

    // Moderation
    public int PendingReports { get; set; }

    // Team
    public List<HubModeratorDto> Moderators { get; set; } = new();

    // Recent Activity
    public List<RecentActivityItemDto> RecentActivity { get; set; } = new();
}

public class HubModeratorDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime AssignedAt { get; set; }
}
