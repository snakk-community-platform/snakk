using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class RestartingModel : SetupPageBase
{
    public void OnGet() => ViewData["SetupStep"] = 11;

    public IActionResult OnPostRestart()
    {
        // Spawn a detached process that restarts all services after a brief delay.
        // The sleep ensures this HTTP response reaches the browser before the process dies.
        // Because supervisord doesn't use stopasgroup/killasgroup, the child sh process
        // survives when supervisord kills the setup dotnet process.
        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = "-c 'sleep 2 && supervisorctl -c /etc/supervisor/conf.d/snakk.conf restart all'",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        });

        return new JsonResult(new { ok = true });
    }
}
