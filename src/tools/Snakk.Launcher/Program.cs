using Snakk.Launcher.Orchestrators;
using Snakk.Launcher.Ui;

var repoRoot = FindRepoRoot();
var docker   = new DockerOrchestrator(repoRoot);
var dotnet   = new DotnetOrchestrator(repoRoot);

#pragma warning disable CS0618
Application.Init();
Application.Run(new MainView(docker, dotnet, Application.Instance!));
Application.Shutdown();
#pragma warning restore CS0618

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir, "CLAUDE.md")))
            return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
