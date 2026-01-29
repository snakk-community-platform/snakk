using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Moderation;

public class ReportsModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Offset { get; set; } = 0;

    public PagedResult<ReportListDto>? Reports { get; set; }
    public bool CanModerate { get; set; }
    public int PageSize => 20;

    public string GetStatusBadgeClass(string status) => status switch
    {
        "Pending" => "badge-warning",
        "Resolved" => "badge-success",
        "Dismissed" => "badge-ghost",
        _ => "badge-ghost"
    };

    public string BuildUrl(string? status = null, int offset = 0)
    {
        var parameters = new List<string>();
        var s = status ?? Status;

        if (!string.IsNullOrEmpty(s)) parameters.Add($"status={s}");
        if (offset > 0) parameters.Add($"offset={offset}");

        return parameters.Count > 0 ? $"/moderation/reports?{string.Join("&", parameters)}" : "/moderation/reports";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        CanModerate = await apiClient.CanModerateAsync();
        if (!CanModerate)
        {
            return RedirectToPage("/Index");
        }

        Reports = await apiClient.GetReportsAsync(Status, Offset, PageSize);
        return Page();
    }
}
