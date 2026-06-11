namespace Snakk.Launcher.Models;

enum LaunchMode { Docker, Local }

record ServiceDefinition(
    string Name,
    string DockerServiceName,
    string? ProjectRelativePath,  // null = docker-only, no dotnet run
    int[] Ports
)
{
    public bool IsDockerOnly => ProjectRelativePath is null;

    public static readonly ServiceDefinition[] DockerServices =
    [
        new("postgres", "postgres", null,                              []),
        new("snakk",    "snakk",   null,                              [17000]),
        new("valkey",   "valkey",  null,                              []),
    ];

    public static readonly ServiceDefinition[] LocalServices =
    [
        new("postgres",        "postgres", null,                                      [5432]),
        new("valkey",          "valkey",   null,                                      [6379]),
        new("Snakk.Api",       "snakk",   @"src\services\Snakk.Api",                 [17100]),
        new("Snakk.Gateway",   "snakk",   @"src\services\Snakk.Gateway",             [17000]),
        new("Snakk.Realtime",  "snakk",   @"src\services\Snakk.Realtime",            [17101]),
        new("Snakk.Auth",      "snakk",   @"src\apps\Snakk.Auth",                   [5100]),
        new("Snakk.Web",       "snakk",   @"src\apps\Snakk.Web",                    [5000]),
        new("Snakk.Admin",     "snakk",   @"src\apps\Snakk.Admin",                  [5001]),
        new("Snakk.Worker",    "snakk",   @"src\services\Snakk.Worker",              []),
    ];
}
