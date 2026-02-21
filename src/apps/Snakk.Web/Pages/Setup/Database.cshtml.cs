using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Setup;

public class DatabaseModel : SetupPageBase
{
    private readonly SetupService _setupService;

    public DatabaseModel(SetupService setupService) => _setupService = setupService;

    [BindProperty] public string DbHost { get; set; } = "localhost";
    [BindProperty] public int DbPort { get; set; } = 5432;
    [BindProperty] public string DbName { get; set; } = "snakk";
    [BindProperty] public string DbUsername { get; set; } = "snakk";
    [BindProperty] public string DbPassword { get; set; } = "";

    public void OnGet()
    {
        ViewData["SetupStep"] = 2;
        var state = GetState();
        DbHost = state.DbHost;
        DbPort = state.DbPort;
        DbName = state.DbName;
        DbUsername = state.DbUsername;
        DbPassword = state.DbPassword;
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 2;
        var state = GetState();
        state.DbHost = DbHost;
        state.DbPort = DbPort;
        state.DbName = DbName;
        state.DbUsername = DbUsername;
        state.DbPassword = DbPassword;
        SaveState(state);
        return RedirectToPage("SiteConfig");
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        var connStr = $"Host={DbHost};Port={DbPort};Database={DbName};Username={DbUsername};Password={DbPassword}";
        var error = await _setupService.TestDatabaseConnectionAsync(connStr);
        return new JsonResult(new { success = error == null, error });
    }
}
