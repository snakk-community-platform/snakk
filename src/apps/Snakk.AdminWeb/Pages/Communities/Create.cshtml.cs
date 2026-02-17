using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.AdminWeb.Services;

namespace Snakk.AdminWeb.Pages.Communities;

[Authorize(Roles = "GlobalAdmin")]
public class CreateModel : PageModel
{
    private readonly AdminApiClientService _apiClientService;
    private readonly ILogger<CreateModel> _logger;

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

    public CreateModel(AdminApiClientService apiClientService, ILogger<CreateModel> logger)
    {
        _apiClientService = apiClientService;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // TODO: Call API to create community
            // var request = new CreateCommunityRequest
            // {
            //     Name = Name,
            //     Slug = Slug,
            //     Description = Description,
            //     Icon = Icon,
            //     Banner = Banner,
            //     IsPrivate = IsPrivate
            // };
            // await _apiClientService.Client.CreateCommunityAsync(request);

            TempData["SuccessMessage"] = $"Community '{Name}' created successfully!";
            return RedirectToPage("/Communities/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create community {Name}", Name);
            ModelState.AddModelError(string.Empty, "Failed to create community. Please try again.");
            return Page();
        }
    }
}
