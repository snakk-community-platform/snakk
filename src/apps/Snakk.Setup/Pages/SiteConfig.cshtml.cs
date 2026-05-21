using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class SiteConfigModel : SetupPageBase
{
    [BindProperty] public string Domain { get; set; } = "";
    [BindProperty] public string SiteName { get; set; } = "Snakk";
    [BindProperty] public bool MultiCommunityEnabled { get; set; }
    [BindProperty] public string Timezone { get; set; } = "UTC";
    [BindProperty] public string Language { get; set; } = "en";

    public void OnGet()
    {
        ViewData["SetupStep"] = 3;
        var state = GetState();
        Domain = !string.IsNullOrEmpty(state.Domain) ? state.Domain : HttpContext.Request.Host.Host;
        SiteName = state.SiteName;
        MultiCommunityEnabled = state.MultiCommunityEnabled;
        Timezone = state.Timezone;
        Language = !string.IsNullOrEmpty(state.Language) ? state.Language : DetectLanguageFromRequest();
    }

    private string DetectLanguageFromRequest()
    {
        var accept = HttpContext.Request.Headers.AcceptLanguage.ToString();
        if (accept.Contains("nb", StringComparison.OrdinalIgnoreCase)
            || accept.Contains("no", StringComparison.OrdinalIgnoreCase)
            || accept.Contains("nn", StringComparison.OrdinalIgnoreCase))
            return "nb";
        return "en";
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 3;
        if (string.IsNullOrWhiteSpace(Domain))
        {
            ModelState.AddModelError("Domain", "Domain is required.");
            return Page();
        }

        var state = GetState();
        state.Domain = Domain.Trim();
        state.SiteName = SiteName.Trim();
        state.MultiCommunityEnabled = MultiCommunityEnabled;
        state.Timezone = string.IsNullOrWhiteSpace(Timezone) ? "UTC" : Timezone;
        state.Language = string.IsNullOrWhiteSpace(Language) ? "en" : Language;
        SaveState(state);

        return RedirectToPage("UserConfig");
    }
}
