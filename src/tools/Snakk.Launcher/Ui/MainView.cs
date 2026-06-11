#pragma warning disable CS0618  // TextView is deprecated but sufficient for a read-only log panel
namespace Snakk.Launcher.Ui;

using Snakk.Launcher.Models;
using Snakk.Launcher.Orchestrators;

class MainView : Window
{
    readonly DockerOrchestrator _docker;
    readonly DotnetOrchestrator _dotnet;
    readonly IApplication _app;
    LaunchMode _mode = LaunchMode.Docker;

    FrameView? _servicesFrame;
    TextView?  _logView;

    record ServiceRow(ServiceDefinition Def, Label StatusLbl, Button ActionBtn);
    readonly List<ServiceRow> _rows = new();

    Timer? _pollTimer;

    public MainView(DockerOrchestrator docker, DotnetOrchestrator dotnet, IApplication app)
    {
        _docker = docker;
        _dotnet = dotnet;
        _app    = app;
        Title   = "Snakk Launcher";

        BuildUi();
        _pollTimer = new Timer(_ => _ = RefreshStatusAsync(), null, 500, 2000);
    }

    void BuildUi()
    {
        var dockerBtn = new Button { Text = "Docker",   X = 1, Y = 0 };
        var localBtn  = new Button { Text = "Local",    X = Pos.Right(dockerBtn) + 1, Y = 0 };
        var startAll  = new Button { Text = "Start All", X = Pos.Right(localBtn) + 3, Y = 0 };
        var stopAll   = new Button { Text = "Stop All",  X = Pos.Right(startAll) + 1, Y = 0 };
        var wipe      = new Button { Text = "Wipe -v",   X = Pos.Right(stopAll) + 1,  Y = 0 };
        var quit      = new Button { Text = "Quit",      X = Pos.Right(wipe) + 1,     Y = 0 };

        dockerBtn.Accepting += (_, _) => SetMode(LaunchMode.Docker);
        localBtn.Accepting  += (_, _) => SetMode(LaunchMode.Local);
        startAll.Accepting  += (_, _) => _ = StartAllAsync();
        stopAll.Accepting   += (_, _) => _ = StopAllAsync();
        wipe.Accepting      += (_, _) => _ = WipeAsync();
        quit.Accepting      += (_, _) => _app.RequestStop(this);

        Add(dockerBtn, localBtn, startAll, stopAll, wipe, quit);

        _servicesFrame = new FrameView
        {
            Title = "Services",
            X = 0, Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Percent(55),
        };
        BuildServiceRows();
        Add(_servicesFrame);

        var logFrame = new FrameView
        {
            Title = "Logs",
            X = 0, Y = Pos.Bottom(_servicesFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _logView = new TextView
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false,
        };
        logFrame.Add(_logView);
        Add(logFrame);
    }

    void BuildServiceRows()
    {
        _servicesFrame!.RemoveAll();
        _rows.Clear();

        var services = _mode == LaunchMode.Docker
            ? ServiceDefinition.DockerServices
            : ServiceDefinition.LocalServices;

        _servicesFrame.Add(new Label
        {
            Text = $"{"Name",-22}{"Status",-14}{"Ports",-14}Action",
            X = 0, Y = 0,
        });

        for (int i = 0; i < services.Length; i++)
        {
            var def  = services[i];
            var rowY = i + 1;

            var ports = def.Ports.Length > 0
                ? string.Join(" ", def.Ports.Select(p => $":{p}"))
                : "";

            var nameLbl   = new Label { Text = def.Name.PadRight(22),          X = 0,  Y = rowY };
            var statusLbl = new Label { Text = "? Unknown ".PadRight(14),       X = 22, Y = rowY };
            var portLbl   = new Label { Text = ports.PadRight(14),              X = 36, Y = rowY };
            var actionBtn = new Button { Text = "Start", X = 50, Y = rowY };

            var captured = def;
            actionBtn.Accepting += (_, _) => _ = ToggleServiceAsync(captured);

            _servicesFrame.Add(nameLbl, statusLbl, portLbl, actionBtn);
            _rows.Add(new ServiceRow(def, statusLbl, actionBtn));
        }
    }

    void SetMode(LaunchMode mode)
    {
        if (_mode == mode) return;
        _dotnet.StopAll();
        _mode = mode;
        _docker.UseLocalOverride = mode == LaunchMode.Local;
        _app.Invoke(BuildServiceRows);
    }

    async Task RefreshStatusAsync()
    {
        if (_mode == LaunchMode.Docker)
        {
            HashSet<string> running;
            try { running = await _docker.GetRunningServicesAsync(); }
            catch { return; }

            _app.Invoke(() =>
            {
                foreach (var row in _rows)
                {
                    var up     = running.Contains(row.Def.DockerServiceName);
                    var status = up ? ServiceStatus.Running : ServiceStatus.Stopped;
                    row.StatusLbl.Text = status.ToLabel().PadRight(14);
                    row.ActionBtn.Text = up ? "Stop " : "Start";
                }
            });
        }
        else
        {
            HashSet<string> dockerRunning;
            try { dockerRunning = await _docker.GetRunningServicesAsync(); }
            catch { return; }

            _app.Invoke(() =>
            {
                foreach (var row in _rows)
                {
                    if (row.Def.IsDockerOnly)
                    {
                        var up     = dockerRunning.Contains(row.Def.DockerServiceName);
                        var status = up ? ServiceStatus.Running : ServiceStatus.Stopped;
                        row.StatusLbl.Text    = status.ToLabel().PadRight(14);
                        row.ActionBtn.Text    = up ? "Stop " : "Start";
                        row.ActionBtn.Enabled = true;
                        continue;
                    }
                    var dotnetUp     = _dotnet.IsRunning(row.Def.Name);
                    var dotnetStatus = dotnetUp ? ServiceStatus.Running : ServiceStatus.Stopped;
                    row.StatusLbl.Text    = dotnetStatus.ToLabel().PadRight(14);
                    row.ActionBtn.Text    = dotnetUp ? "Stop " : "Start";
                    row.ActionBtn.Enabled = true;
                }
            });
        }
    }

    async Task ToggleServiceAsync(ServiceDefinition def)
    {
        if (_mode == LaunchMode.Docker)
        {
            var running = (await _docker.GetRunningServicesAsync()).Contains(def.DockerServiceName);
            if (running)
                await _docker.StopAsync(def.DockerServiceName, AppendLog);
            else
                await _docker.UpAsync(def.DockerServiceName, AppendLog);
        }
        else
        {
            if (def.IsDockerOnly)
            {
                var running = (await _docker.GetRunningServicesAsync()).Contains(def.DockerServiceName);
                if (running)
                    await _docker.StopAsync(def.DockerServiceName, AppendLog);
                else
                    await _docker.UpAsync(def.DockerServiceName, AppendLog);
            }
            else if (_dotnet.IsRunning(def.Name))
                _dotnet.Stop(def.Name);
            else
                _dotnet.Start(def, AppendLog);
        }
        await RefreshStatusAsync();
    }

    async Task StartAllAsync()
    {
        if (_mode == LaunchMode.Docker)
        {
            await _docker.UpAllAsync(AppendLog);
        }
        else
        {
            var dockerServices = string.Join(" ", _rows
                .Where(r => r.Def.IsDockerOnly)
                .Select(r => r.Def.DockerServiceName));
            if (!string.IsNullOrEmpty(dockerServices))
                await _docker.UpAsync(dockerServices, AppendLog);

            foreach (var row in _rows.Where(r => !r.Def.IsDockerOnly))
                _dotnet.Start(row.Def, AppendLog);
        }
        await RefreshStatusAsync();
    }

    async Task StopAllAsync()
    {
        if (_mode == LaunchMode.Docker)
        {
            await _docker.StopAllAsync(AppendLog);
        }
        else
        {
            _dotnet.StopAll();

            var dockerServices = string.Join(" ", _rows
                .Where(r => r.Def.IsDockerOnly)
                .Select(r => r.Def.DockerServiceName));
            if (!string.IsNullOrEmpty(dockerServices))
                await _docker.StopAsync(dockerServices, AppendLog);
        }
        await RefreshStatusAsync();
    }

    async Task WipeAsync()
    {
        if (_mode != LaunchMode.Docker) return;
        _dotnet.StopAll();
        await _docker.WipeAsync(AppendLog);
        await RefreshStatusAsync();
    }

    void AppendLog(string line)
    {
        _app.Invoke(() =>
        {
            if (_logView is null) return;
            _logView.Text += line + "\n";
        });
    }

    protected override void Dispose(bool disposing)
    {
        _pollTimer?.Dispose();
        _dotnet.StopAll();
        base.Dispose(disposing);
    }
}
