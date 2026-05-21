using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class UserConfigModel : SetupPageBase
{
    [BindProperty] public bool PasskeysEnabled { get; set; } = true;
    [BindProperty] public bool TwoFactorEnabled { get; set; } = true;
    [BindProperty] public List<string> AllowedDisplayNameScripts { get; set; } = ["Latin"];

    public void OnGet()
    {
        ViewData["SetupStep"] = 4;
        var state = GetState();
        PasskeysEnabled = state.PasskeysEnabled;
        TwoFactorEnabled = state.TwoFactorEnabled;
        AllowedDisplayNameScripts = state.AllowedDisplayNameScripts.Count > 0
            ? state.AllowedDisplayNameScripts
            : ["Latin"];
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 4;
        var state = GetState();
        state.PasskeysEnabled = PasskeysEnabled;
        state.TwoFactorEnabled = TwoFactorEnabled;
        state.AllowedDisplayNameScripts = AllowedDisplayNameScripts.Count > 0
            ? AllowedDisplayNameScripts
            : ["Latin"];
        SaveState(state);

        return RedirectToPage("Storage");
    }
}
