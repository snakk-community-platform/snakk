namespace Snakk.Setup.Pages;

public class RestartingModel : SetupPageBase
{
    public void OnGet() => ViewData["SetupStep"] = 13;

    // No restart action needed — the gateway detects conf/snakk-config.json
    // via FileSystemWatcher and starts routing to the web app automatically.
    // In Docker, entrypoint.sh also watches for this file and starts services.
}
