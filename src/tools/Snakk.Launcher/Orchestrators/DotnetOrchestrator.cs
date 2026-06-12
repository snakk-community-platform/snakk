namespace Snakk.Launcher.Orchestrators;

using System.Diagnostics;
using Snakk.Launcher.Models;

class DotnetOrchestrator(string repoRoot)
{
    readonly Dictionary<string, Process> _procs = new();

    public bool IsRunning(string name) =>
        _procs.TryGetValue(name, out var p) && !p.HasExited;

    public void Start(ServiceDefinition def, Action<string> log)
    {
        if (def.IsDockerOnly || IsRunning(def.Name)) return;

        var projDir = Path.Combine(repoRoot, def.ProjectRelativePath!);
        if (!Directory.Exists(projDir))
        {
            log($"[{def.Name}] project path not found: {projDir}");
            return;
        }

        var proc = ProcessRunner.Start("dotnet", "run", projDir, line => log($"[{def.Name}] {line}"));
        _procs[def.Name] = proc;
    }

    public void Stop(string name)
    {
        if (_procs.TryGetValue(name, out var proc))
        {
            if (!proc.HasExited)
                proc.Kill(entireProcessTree: true);
            proc.Dispose();
            _procs.Remove(name);
        }
    }

    public void StopAll()
    {
        foreach (var name in _procs.Keys.ToList())
            Stop(name);
    }
}
