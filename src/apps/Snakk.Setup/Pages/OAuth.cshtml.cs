using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class OAuthModel : SetupPageBase
{
    [BindProperty] public string GoogleClientId { get; set; } = "";
    [BindProperty] public string GoogleClientSecret { get; set; } = "";
    [BindProperty] public string GitHubClientId { get; set; } = "";
    [BindProperty] public string GitHubClientSecret { get; set; } = "";
    [BindProperty] public string DiscordClientId { get; set; } = "";
    [BindProperty] public string DiscordClientSecret { get; set; } = "";
    [BindProperty] public string FacebookClientId { get; set; } = "";
    [BindProperty] public string FacebookClientSecret { get; set; } = "";
    [BindProperty] public string MicrosoftClientId { get; set; } = "";
    [BindProperty] public string MicrosoftClientSecret { get; set; } = "";
    [BindProperty] public string SteamApiKey { get; set; } = "";

    public void OnGet()
    {
        ViewData["SetupStep"] = 9;
        var state = GetState();
        GoogleClientId = state.GoogleClientId;
        GoogleClientSecret = state.GoogleClientSecret;
        GitHubClientId = state.GitHubClientId;
        GitHubClientSecret = state.GitHubClientSecret;
        DiscordClientId = state.DiscordClientId;
        DiscordClientSecret = state.DiscordClientSecret;
        FacebookClientId = state.FacebookClientId;
        FacebookClientSecret = state.FacebookClientSecret;
        MicrosoftClientId = state.MicrosoftClientId;
        MicrosoftClientSecret = state.MicrosoftClientSecret;
        SteamApiKey = state.SteamApiKey;
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
        state.FacebookClientId = FacebookClientId?.Trim() ?? "";
        state.FacebookClientSecret = FacebookClientSecret?.Trim() ?? "";
        state.MicrosoftClientId = MicrosoftClientId?.Trim() ?? "";
        state.MicrosoftClientSecret = MicrosoftClientSecret?.Trim() ?? "";
        state.SteamApiKey = SteamApiKey?.Trim() ?? "";
        SaveState(state);

        return RedirectToPage("Community");
    }
}
