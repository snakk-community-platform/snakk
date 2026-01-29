using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Moderation;

public class IndexModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public IEnumerable<UserRoleDto>? MyRoles { get; set; }
    public PagedResult<ReportListDto>? PendingReports { get; set; }
    public PagedResult<ModerationLogDto>? RecentLogs { get; set; }
    public int PendingReportCount { get; set; }
    public bool CanModerate { get; set; }

    public string GetRoleDisplayName(string role) => role switch
    {
        "GlobalAdmin" => "Global Admin",
        "CommunityAdmin" => "Community Admin",
        "CommunityMod" => "Community Moderator",
        "HubMod" => "Hub Moderator",
        "SpaceMod" => "Space Moderator",
        _ => role
    };

    public string GetRoleBadgeClass(string role) => role switch
    {
        "GlobalAdmin" => "badge-error",
        "CommunityAdmin" => "badge-warning",
        "CommunityMod" => "badge-info",
        "HubMod" => "badge-primary",
        "SpaceMod" => "badge-secondary",
        _ => "badge-ghost"
    };

    public string GetActionDisplayName(string action) => action switch
    {
        "AssignRole" => "Assigned role",
        "RevokeRole" => "Revoked role",
        "BanUser" => "Banned user",
        "UnbanUser" => "Unbanned user",
        "DeletePost" => "Deleted post",
        "DeleteDiscussion" => "Deleted discussion",
        "LockDiscussion" => "Locked discussion",
        "UnlockDiscussion" => "Unlocked discussion",
        "ResolveReport" => "Resolved report",
        "DismissReport" => "Dismissed report",
        _ => action
    };

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user can moderate
        CanModerate = await apiClient.CanModerateAsync();

        if (!CanModerate)
        {
            return RedirectToPage("/Index");
        }

        // Fetch data in parallel
        var rolesTask = apiClient.GetMyRolesAsync();
        var reportsTask = apiClient.GetReportsAsync("Pending", 0, 10);
        var logsTask = apiClient.GetModerationLogsAsync(offset: 0, pageSize: 10);
        var countTask = apiClient.GetPendingReportCountAsync();

        await Task.WhenAll(rolesTask, reportsTask, logsTask);

        MyRoles = await rolesTask;
        PendingReports = await reportsTask;
        RecentLogs = await logsTask;
        PendingReportCount = await countTask;

        return Page();
    }
}
