using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Moderation;

public class ReportDetailModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public ReportDetailDto? Report { get; set; }
    public bool CanModerate { get; set; }

    [BindProperty]
    public string? CommentContent { get; set; }

    [BindProperty]
    public string? ResolutionNote { get; set; }

    public new string GetRelativeTime(DateTime dateTime)
    {
        var diff = DateTime.UtcNow - dateTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dateTime.ToString("MMM d, yyyy 'at' h:mm tt");
    }

    public string GetStatusBadgeClass(string status) => status switch
    {
        "Pending" => "badge-warning",
        "Resolved" => "badge-success",
        "Dismissed" => "badge-ghost",
        _ => "badge-ghost"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        CanModerate = await apiClient.CanModerateAsync();
        if (!CanModerate)
        {
            return RedirectToPage("/Index");
        }

        Report = await apiClient.GetReportDetailAsync(Id);
        if (Report == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(CommentContent))
        {
            return RedirectToPage(new { Id });
        }

        await apiClient.AddReportCommentAsync(Id, new AddReportCommentRequest(CommentContent));
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostResolveAsync()
    {
        await apiClient.ResolveReportAsync(Id, new ResolveReportRequest(ResolutionNote, false));
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostDismissAsync()
    {
        await apiClient.ResolveReportAsync(Id, new ResolveReportRequest(ResolutionNote, true));
        return RedirectToPage(new { Id });
    }
}
