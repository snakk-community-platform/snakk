using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class CommunityModel : SetupPageBase
{
    [BindProperty] public bool CreateFirstCommunity { get; set; } = true;
    [BindProperty] public string CommunityName { get; set; } = "";
    [BindProperty] public string CommunitySlug { get; set; } = "main";
    [BindProperty] public string CommunityDescription { get; set; } = "";
    [BindProperty] public string FirstHubName { get; set; } = "";
    [BindProperty] public string FirstHubSlug { get; set; } = "";
    [BindProperty] public string FirstSpaceName { get; set; } = "";
    [BindProperty] public string FirstSpaceSlug { get; set; } = "";

    public void OnGet()
    {
        ViewData["SetupStep"] = 10;
        var state = GetState();
        CreateFirstCommunity = state.CreateFirstCommunity;
        CommunityName = state.CommunityName;
        CommunitySlug = state.DefaultCommunitySlug;
        CommunityDescription = state.CommunityDescription;
        FirstHubName = state.FirstHubName;
        FirstHubSlug = state.FirstHubSlug;
        FirstSpaceName = state.FirstSpaceName;
        FirstSpaceSlug = state.FirstSpaceSlug;

        // Pre-fill from site config if empty
        if (string.IsNullOrEmpty(CommunityName))
            CommunityName = state.SiteName;
        if (string.IsNullOrEmpty(FirstHubName))
            FirstHubName = "General";
        if (string.IsNullOrEmpty(FirstHubSlug))
            FirstHubSlug = "general";
        if (string.IsNullOrEmpty(FirstSpaceName))
            FirstSpaceName = "Discussions";
        if (string.IsNullOrEmpty(FirstSpaceSlug))
            FirstSpaceSlug = "discussions";
    }

    public IActionResult OnPost()
    {
        var state = GetState();
        state.CreateFirstCommunity = CreateFirstCommunity;

        if (CreateFirstCommunity)
        {
            state.CommunityName = CommunityName?.Trim() ?? "";
            state.DefaultCommunitySlug = CommunitySlug?.Trim().ToLowerInvariant() ?? "main";
            state.CommunityDescription = CommunityDescription?.Trim() ?? "";
            state.FirstHubName = FirstHubName?.Trim() ?? "";
            state.FirstHubSlug = FirstHubSlug?.Trim().ToLower() ?? "";
            state.FirstSpaceName = FirstSpaceName?.Trim() ?? "";
            state.FirstSpaceSlug = FirstSpaceSlug?.Trim().ToLower() ?? "";
        }

        SaveState(state);
        return RedirectToPage("TestData");
    }
}
