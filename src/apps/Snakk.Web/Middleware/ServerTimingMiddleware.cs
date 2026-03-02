using System.Diagnostics;

namespace Snakk.Web.Middleware;

public class ServerTimingCollector(Stopwatch requestStopwatch)
{
    private readonly List<string> _entries = new();

    /// <summary>
    /// Returns the elapsed milliseconds since the request started.
    /// Call this BEFORE the operation to record when it was initiated.
    /// </summary>
    public long GetOffsetMs() => requestStopwatch.ElapsedMilliseconds;

    public void Add(string name, double durationMs, string? description = null, long? offsetMs = null)
    {
        var offsetTag = offsetMs.HasValue ? $" @{offsetMs.Value}" : "";
        var entry = description is not null
            ? $"{name};dur={durationMs:F1};desc=\"{description}{offsetTag}\""
            : $"{name};dur={durationMs:F1}";
        _entries.Add(entry);
    }

    public string ToHeaderValue() => string.Join(", ", _entries);
    public bool HasEntries => _entries.Count > 0;
}

public class ServerTimingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var collector = new ServerTimingCollector(sw);
        context.Items["ServerTiming"] = collector;

        context.Response.OnStarting(() =>
        {
            sw.Stop();
            collector.Add("total", sw.ElapsedMilliseconds, "Server");

            if (collector.HasEntries)
            {
                context.Response.Headers["Server-Timing"] = collector.ToHeaderValue();
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
