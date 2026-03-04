using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Setup.Services;

namespace Snakk.Setup.Pages;

/// <summary>
/// Base class for setup wizard pages. Manages SetupState in session.
/// </summary>
public abstract class SetupPageBase : PageModel
{
    private const string SessionKey = "SetupState";

    protected SetupState GetState()
    {
        var json = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json))
            return new SetupState();
        return JsonSerializer.Deserialize<SetupState>(json) ?? new SetupState();
    }

    protected void SaveState(SetupState state)
    {
        var json = JsonSerializer.Serialize(state);
        HttpContext.Session.SetString(SessionKey, json);
    }
}
