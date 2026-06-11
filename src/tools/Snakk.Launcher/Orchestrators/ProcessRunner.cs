namespace Snakk.Launcher.Orchestrators;

using System.Diagnostics;

static class ProcessRunner
{
    public static Process Start(string exe, string args, string workDir, Action<string> onOutput)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return proc;
    }

    public static async Task<int> RunAsync(string exe, string args, string workDir, Action<string> onOutput,
        CancellationToken ct = default)
    {
        var proc = Start(exe, args, workDir, onOutput);
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode;
    }
}
