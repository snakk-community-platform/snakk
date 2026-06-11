namespace Snakk.Launcher.Models;

enum ServiceStatus { Unknown, Stopped, Starting, Running, Stopping, Error }

static class ServiceStatusExtensions
{
    public static string ToLabel(this ServiceStatus s) => s switch
    {
        ServiceStatus.Running  => "● Running ",
        ServiceStatus.Starting => "◌ Starting",
        ServiceStatus.Stopping => "◌ Stopping",
        ServiceStatus.Stopped  => "○ Stopped ",
        ServiceStatus.Error    => "✗ Error   ",
        _                      => "? Unknown ",
    };
}
