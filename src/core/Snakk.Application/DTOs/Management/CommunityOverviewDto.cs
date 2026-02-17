namespace Snakk.Application.DTOs.Management;

public class CommunityOverviewDto
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    // Stats
    public int TotalMembers { get; set; }
    public int TotalHubs { get; set; }
    public int TotalSpaces { get; set; }
    public int TotalDiscussions { get; set; }
    public int TotalPosts { get; set; }

    // Activity
    public int NewMembersToday { get; set; }
    public int NewMembersThisWeek { get; set; }
    public int PostsToday { get; set; }
    public int PostsThisWeek { get; set; }

    // Moderation
    public int PendingReports { get; set; }
    public int ActiveBans { get; set; }

    // Team
    public CommunityMemberDto? Owner { get; set; }
    public List<CommunityMemberDto> Admins { get; set; } = new();
    public List<CommunityMemberDto> Moderators { get; set; } = new();

    // Recent Activity
    public List<RecentActivityItemDto> RecentActivity { get; set; } = new();
}

public class RecentActivityItemDto
{
    public string Type { get; set; } = string.Empty; // "discussion", "post", "member_join", "report", etc.
    public string Description { get; set; } = string.Empty;
    public string? UserDisplayName { get; set; }
    public string? UserAvatarUrl { get; set; }
    public DateTime Timestamp { get; set; }
    public string? LinkUrl { get; set; }
}
