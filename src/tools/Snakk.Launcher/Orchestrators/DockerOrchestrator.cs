namespace Snakk.Launcher.Orchestrators;

class DockerOrchestrator(string repoRoot)
{
    string ComposeDir        => Path.Combine(repoRoot, "docker");
    string ComposeFile       => Path.Combine(ComposeDir, "docker-compose.yml");
    string LocalOverrideFile => Path.Combine(ComposeDir, "docker-compose.local.yml");

    public bool UseLocalOverride { get; set; }

    string ComposeArgs => UseLocalOverride && File.Exists(LocalOverrideFile)
        ? $"compose -f \"{ComposeFile}\" -f \"{LocalOverrideFile}\""
        : $"compose -f \"{ComposeFile}\"";

    public Task<int> UpAsync(string service, Action<string> log) =>
        Compose($"up -d {service}", log);

    public Task<int> StopAsync(string service, Action<string> log) =>
        Compose($"stop {service}", log);

    public Task<int> BuildAsync(string service, Action<string> log) =>
        Compose($"build {service}", log);

    public Task<int> UpAllAsync(Action<string> log) =>
        Compose("up -d", log);

    public Task<int> StopAllAsync(Action<string> log) =>
        Compose("stop", log);

    public Task<int> WipeAsync(Action<string> log) =>
        Compose("down -v --remove-orphans", log);

    public async Task<HashSet<string>> GetRunningServicesAsync()
    {
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await ProcessRunner.RunAsync("docker", $"{ComposeArgs} ps --services --status running",
            ComposeDir, line => { if (!string.IsNullOrWhiteSpace(line)) running.Add(line.Trim()); });
        return running;
    }

    Task<int> Compose(string args, Action<string> log) =>
        ProcessRunner.RunAsync("docker", $"{ComposeArgs} {args}", ComposeDir, log);
}
