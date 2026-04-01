using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class EmailModel : SetupPageBase
{
    [BindProperty] public bool SmtpEnabled { get; set; }
    [BindProperty] public string SmtpHost { get; set; } = "";
    [BindProperty] public int SmtpPort { get; set; } = 587;
    [BindProperty] public string SmtpUsername { get; set; } = "";
    [BindProperty] public string SmtpPassword { get; set; } = "";
    [BindProperty] public string SmtpSenderEmail { get; set; } = "";
    [BindProperty] public string SmtpSenderName { get; set; } = "Snakk";

    public void OnGet()
    {
        ViewData["SetupStep"] = 6;
        var state = GetState();
        SmtpEnabled = state.SmtpEnabled;
        SmtpHost = state.SmtpHost;
        SmtpPort = state.SmtpPort;
        SmtpUsername = state.SmtpUsername;
        SmtpPassword = state.SmtpPassword;
        SmtpSenderEmail = state.SmtpSenderEmail;
        SmtpSenderName = state.SmtpSenderName;
    }

    public IActionResult OnPost()
    {
        var state = GetState();
        state.SmtpEnabled = SmtpEnabled;
        state.SmtpHost = SmtpHost?.Trim() ?? "";
        state.SmtpPort = SmtpPort;
        state.SmtpUsername = SmtpUsername?.Trim() ?? "";
        state.SmtpPassword = SmtpPassword ?? "";
        state.SmtpSenderEmail = SmtpSenderEmail?.Trim() ?? "";
        state.SmtpSenderName = SmtpSenderName?.Trim() ?? "Snakk";
        SaveState(state);
        return RedirectToPage("Security");
    }
}
