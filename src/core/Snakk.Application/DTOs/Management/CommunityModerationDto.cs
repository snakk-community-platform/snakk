namespace Snakk.Application.DTOs.Management;

public class CommunityModerationDto
{
    public List<ModerationReportDto> PendingReports { get; set; } = new();
    public List<ModerationActionDto> RecentActions { get; set; } = new();
    public List<BannedUserDto> BannedUsers { get; set; } = new();
    public ModerationStatsDto Stats { get; set; } = new();
}

public class ModerationReportDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty; // "Post", "Discussion", "User"
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ReportedByUserId { get; set; } = string.Empty;
    public string ReportedByDisplayName { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string? TargetUserDisplayName { get; set; }
    public int? TargetPostId { get; set; }
    public int? TargetDiscussionId { get; set; }
    public string Status { get; set; } = string.Empty; // "Pending", "Resolved", "Dismissed"
    public DateTime CreatedAt { get; set; }
    public string? ContentPreview { get; set; }
}

public class ModerationActionDto
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty; // "Ban", "DeletePost", "DeleteDiscussion", "Warning"
    public string ModeratorDisplayName { get; set; } = string.Empty;
    public string? TargetUserDisplayName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class BannedUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime BannedAt { get; set; }
    public string BannedByDisplayName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public bool IsPermanent { get; set; }
}

public class ModerationStatsDto
{
    public int TotalReports { get; set; }
    public int PendingReports { get; set; }
    public int ResolvedReports { get; set; }
    public int DismissedReports { get; set; }
    public int TotalBans { get; set; }
    public int ActiveBans { get; set; }
    public int ActionsThisWeek { get; set; }
}
