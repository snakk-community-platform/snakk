using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.AdminWeb.Pages;

[Authorize]
public class IndexModel : PageModel
{
    public DashboardStats Stats { get; set; } = new();
    public List<RecentActivityItem> RecentActivity { get; set; } = new();

    public void OnGet()
    {
        // TODO: Fetch real stats from API
        Stats = new DashboardStats
        {
            TotalUsers = 1250,
            TotalCommunities = 42,
            TotalPosts = 8934,
            PendingReports = 7
        };

        // TODO: Fetch real recent activity from API
        RecentActivity = new List<RecentActivityItem>
        {
            new() { Timestamp = DateTime.UtcNow.AddMinutes(-5), Description = "New community created: Tech Hub", Username = "admin" },
            new() { Timestamp = DateTime.UtcNow.AddMinutes(-12), Description = "User banned: spammer123", Username = "moderator1" },
            new() { Timestamp = DateTime.UtcNow.AddMinutes(-23), Description = "Report resolved", Username = "moderator2" },
        };
    }

    public record DashboardStats
    {
        public int TotalUsers { get; init; }
        public int TotalCommunities { get; init; }
        public int TotalPosts { get; init; }
        public int PendingReports { get; init; }
    }

    public record RecentActivityItem
    {
        public DateTime Timestamp { get; init; }
        public string Description { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
    }
}
