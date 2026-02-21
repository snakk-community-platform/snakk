using Microsoft.AspNetCore.Mvc;

namespace Snakk.Web.Pages.Setup;

public class OAuthModel : SetupPageBase
{
    [BindProperty] public string GoogleClientId { get; set; } = "";
    [BindProperty] public string GoogleClientSecret { get; set; } = "";
    [BindProperty] public string GitHubClientId { get; set; } = "";
    [BindProperty] public string GitHubClientSecret { get; set; } = "";
    [BindProperty] public string DiscordClientId { get; set; } = "";
    [BindProperty] public string DiscordClientSecret { get; set; } = "";

    public void OnGet()
    {
        ViewData["SetupStep"] = 8;
        var state = GetState();
        GoogleClientId = state.GoogleClientId;
        GoogleClientSecret = state.GoogleClientSecret;
        GitHubClientId = state.GitHubClientId;
        GitHubClientSecret = state.GitHubClientSecret;
        DiscordClientId = state.DiscordClientId;
        DiscordClientSecret = state.DiscordClientSecret;
    }

    public IActionResult OnPost()
    {
        var state = GetState();
        state.GoogleClientId = GoogleClientId?.Trim() ?? "";
        state.GoogleClientSecret = GoogleClientSecret?.Trim() ?? "";
        state.GitHubClientId = GitHubClientId?.Trim() ?? "";
        state.GitHubClientSecret = GitHubClientSecret?.Trim() ?? "";
        state.DiscordClientId = DiscordClientId?.Trim() ?? "";
        state.DiscordClientSecret = DiscordClientSecret?.Trim() ?? "";
        SaveState(state);
        return RedirectToPage("Review");
    }
}
