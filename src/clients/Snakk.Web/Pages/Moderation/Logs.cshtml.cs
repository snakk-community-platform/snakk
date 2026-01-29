using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Moderation;

public class LogsModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    [BindProperty(SupportsGet = true)]
    public string? CommunityId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? HubId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SpaceId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Offset { get; set; } = 0;

    public PagedResult<ModerationLogDto>? Logs { get; set; }
    public bool CanModerate { get; set; }
    public int PageSize => 20;

    public new string GetRelativeTime(DateTime dateTime)
    {
        var diff = DateTime.UtcNow - dateTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dateTime.ToString("MMM d, yyyy 'at' h:mm tt");
    }

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

    public string GetActionBadgeClass(string action) => action switch
    {
        "AssignRole" => "badge-info",
        "RevokeRole" => "badge-warning",
        "BanUser" => "badge-error",
        "UnbanUser" => "badge-success",
        "DeletePost" => "badge-error",
        "DeleteDiscussion" => "badge-error",
        "LockDiscussion" => "badge-warning",
        "UnlockDiscussion" => "badge-info",
        "ResolveReport" => "badge-success",
        "DismissReport" => "badge-ghost",
        _ => "badge-ghost"
    };

    public string BuildUrl(int offset = 0)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrEmpty(CommunityId)) parameters.Add($"communityId={CommunityId}");
        if (!string.IsNullOrEmpty(HubId)) parameters.Add($"hubId={HubId}");
        if (!string.IsNullOrEmpty(SpaceId)) parameters.Add($"spaceId={SpaceId}");
        if (offset > 0) parameters.Add($"offset={offset}");

        return parameters.Count > 0 ? $"/moderation/logs?{string.Join("&", parameters)}" : "/moderation/logs";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        CanModerate = await apiClient.CanModerateAsync();
        if (!CanModerate)
        {
            return RedirectToPage("/Index");
        }

        Logs = await apiClient.GetModerationLogsAsync(CommunityId, HubId, SpaceId, Offset, PageSize);
        return Page();
    }
}
