using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.AdminWeb.Services;

namespace Snakk.AdminWeb.Pages.Communities;

[Authorize(Roles = "GlobalAdmin,CommunityAdmin")]
public class DetailsModel : PageModel
{
    private readonly AdminApiClientService _apiClientService;
    private readonly ILogger<DetailsModel> _logger;

    public CommunityInfo? Community { get; set; }
    public CommunityOverview? Overview { get; set; }
    public List<CommunityMember>? Members { get; set; }
    public ModerationStats? Moderation { get; set; }
    public List<ModerationReport>? ModerationReports { get; set; }

    public DetailsModel(AdminApiClientService apiClientService, ILogger<DetailsModel> logger)
    {
        _apiClientService = apiClientService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string slug, string? tab = "overview")
    {
        try
        {
            // TODO: Fetch from API based on tab
            // var overview = await _apiClientService.Client.GetCommunityOverviewAsync(slug);

            // Mock data for now
            Community = new CommunityInfo
            {
                Name = "Technology",
                Slug = slug,
                Description = "All things tech - programming, gadgets, innovation",
                IsPrivate = false,
                IsActive = true
            };

            Overview = new CommunityOverview
            {
                TotalMembers = 15420,
                TotalPosts = 3421,
                ActiveToday = 342,
                PendingReports = 5,
                RecentActivity = new List<ActivityItem>
                {
                    new() { Timestamp = DateTime.UtcNow.AddMinutes(-5), Description = "New post created", Username = "user123" },
                    new() { Timestamp = DateTime.UtcNow.AddMinutes(-12), Description = "User joined", Username = "newuser" },
                    new() { Timestamp = DateTime.UtcNow.AddHours(-2), Description = "Post reported", Username = "moderator" },
                },
                Team = new List<TeamMember>
                {
                    new() { Username = "admin", Role = "Admin", Since = DateTime.UtcNow.AddMonths(-6) },
                    new() { Username = "mod1", Role = "Moderator", Since = DateTime.UtcNow.AddMonths(-3) },
                }
            };

            Members = new List<CommunityMember>
            {
                new() { Username = "user123", Role = "Member", JoinedAt = DateTime.UtcNow.AddMonths(-2), PostCount = 45 },
                new() { Username = "user456", Role = "Member", JoinedAt = DateTime.UtcNow.AddMonths(-4), PostCount = 123 },
                new() { Username = "poweruser", Role = "Power User", JoinedAt = DateTime.UtcNow.AddMonths(-6), PostCount = 567 },
            };

            Moderation = new ModerationStats
            {
                PendingReports = 5,
                ResolvedReports = 42,
                BannedUsers = 3,
                RemovedPosts = 18
            };

            ModerationReports = new List<ModerationReport>
            {
                new()
                {
                    ItemType = "Post",
                    ItemContent = "This is a spam post selling fake products",
                    Reason = "Spam",
                    ReporterUsername = "user123",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                },
                new()
                {
                    ItemType = "Comment",
                    ItemContent = "Offensive language and harassment",
                    Reason = "Harassment",
                    ReporterUsername = "user456",
                    Status = "Resolved",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
            };

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load community details for {Slug}", slug);
            return RedirectToPage("/Communities/Index");
        }
    }

    public record CommunityInfo
    {
        public string Name { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool IsPrivate { get; init; }
        public bool IsActive { get; init; }
    }

    public record CommunityOverview
    {
        public int TotalMembers { get; init; }
        public int TotalPosts { get; init; }
        public int ActiveToday { get; init; }
        public int PendingReports { get; init; }
        public List<ActivityItem> RecentActivity { get; init; } = new();
        public List<TeamMember> Team { get; init; } = new();
    }

    public record ActivityItem
    {
        public DateTime Timestamp { get; init; }
        public string Description { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
    }

    public record TeamMember
    {
        public string Username { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public DateTime Since { get; init; }
    }

    public record CommunityMember
    {
        public string Username { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public DateTime JoinedAt { get; init; }
        public int PostCount { get; init; }
    }

    public record ModerationStats
    {
        public int PendingReports { get; init; }
        public int ResolvedReports { get; init; }
        public int BannedUsers { get; init; }
        public int RemovedPosts { get; init; }
    }

    public record ModerationReport
    {
        public string ItemType { get; init; } = string.Empty;
        public string ItemContent { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public string ReporterUsername { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }
}
