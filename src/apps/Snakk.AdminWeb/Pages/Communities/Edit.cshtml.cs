using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.AdminWeb.Services;

namespace Snakk.AdminWeb.Pages.Communities;

[Authorize(Roles = "GlobalAdmin,CommunityAdmin")]
public class EditModel : PageModel
{
    private readonly AdminApiClientService _apiClientService;
    private readonly ILogger<EditModel> _logger;

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string Slug { get; set; } = string.Empty;

    [BindProperty]
    public string Description { get; set; } = string.Empty;

    [BindProperty]
    public string? Icon { get; set; }

    [BindProperty]
    public string? Banner { get; set; }

    [BindProperty]
    public bool IsPrivate { get; set; }

    [BindProperty]
    public bool IsActive { get; set; }

    public EditModel(AdminApiClientService apiClientService, ILogger<EditModel> logger)
    {
        _apiClientService = apiClientService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        try
        {
            // TODO: Fetch from API
            // var settings = await _apiClientService.Client.GetCommunitySettingsAsync(slug);
            // if (settings == null)
            // {
            //     return NotFound();
            // }

            // Mock data for now
            Slug = slug;
            Name = "Technology";
            Description = "All things tech - programming, gadgets, innovation";
            Icon = null;
            Banner = null;
            IsPrivate = false;
            IsActive = true;

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load community {Slug}", slug);
            return RedirectToPage("/Communities/Index");
        }
    }

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        Slug = slug;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // TODO: Call API to update community
            // var request = new UpdateCommunitySettingsRequest
            // {
            //     Name = Name,
            //     Description = Description,
            //     Icon = Icon,
            //     Banner = Banner,
            //     IsPrivate = IsPrivate,
            //     IsActive = IsActive
            // };
            // await _apiClientService.Client.UpdateCommunitySettingsAsync(slug, request);

            TempData["SuccessMessage"] = $"Community '{Name}' updated successfully!";
            return RedirectToPage("/Communities/Details", new { slug });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update community {Slug}", slug);
            ModelState.AddModelError(string.Empty, "Failed to update community. Please try again.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string slug)
    {
        try
        {
            // TODO: Call API to delete community
            // await _apiClientService.Client.DeleteCommunityAsync(slug);

            TempData["SuccessMessage"] = "Community deleted successfully!";
            return RedirectToPage("/Communities/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete community {Slug}", slug);
            TempData["ErrorMessage"] = "Failed to delete community. Please try again.";
            return RedirectToPage("/Communities/Edit", new { slug });
        }
    }
}
