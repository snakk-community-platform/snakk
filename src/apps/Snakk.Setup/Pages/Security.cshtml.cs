using Microsoft.AspNetCore.Mvc;
using Snakk.Setup.Services;

namespace Snakk.Setup.Pages;

public class SecurityModel : SetupPageBase
{
    [BindProperty] public string JwtSecretKey { get; set; } = "";
    [BindProperty] public string RealtimeApiKey { get; set; } = "";
    [BindProperty] public string TurnstileSiteKey { get; set; } = "";
    [BindProperty] public string TurnstileSecretKey { get; set; } = "";

    public void OnGet()
    {
        ViewData["SetupStep"] = 7;
        var state = GetState();

        // Auto-generate secrets if not already set
        if (string.IsNullOrEmpty(state.JwtSecretKey))
            state.JwtSecretKey = SetupService.GenerateSecretKey(64);
        if (string.IsNullOrEmpty(state.RealtimeApiKey))
            state.RealtimeApiKey = SetupService.GenerateSecretKey(32);

        SaveState(state);
        JwtSecretKey = state.JwtSecretKey;
        RealtimeApiKey = state.RealtimeApiKey;
        TurnstileSiteKey = state.TurnstileSiteKey;
        TurnstileSecretKey = state.TurnstileSecretKey;
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 7;

        if (string.IsNullOrWhiteSpace(JwtSecretKey) || JwtSecretKey.Length < 32)
        {
            ModelState.AddModelError("JwtSecretKey", "JWT secret must be at least 32 characters.");
            return Page();
        }

        var state = GetState();
        state.JwtSecretKey = JwtSecretKey;
        state.RealtimeApiKey = RealtimeApiKey;
        state.TurnstileSiteKey = TurnstileSiteKey?.Trim() ?? "";
        state.TurnstileSecretKey = TurnstileSecretKey?.Trim() ?? "";
        SaveState(state);

        return RedirectToPage("OAuth");
    }

    public IActionResult OnPostRegenerate()
    {
        var state = GetState();
        state.JwtSecretKey = SetupService.GenerateSecretKey(64);
        state.RealtimeApiKey = SetupService.GenerateSecretKey(32);
        SaveState(state);

        return RedirectToPage("Security");
    }
}
