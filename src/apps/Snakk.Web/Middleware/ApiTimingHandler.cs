using System.Diagnostics;

namespace Snakk.Web.Middleware;

public class ApiTimingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiTimingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var collector = _httpContextAccessor.HttpContext?.Items["ServerTiming"] as ServerTimingCollector;

        var offsetMs = collector?.GetOffsetMs();
        var sw = Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken);
        sw.Stop();

        var path = request.RequestUri?.PathAndQuery ?? "unknown";
        var method = request.Method.Method;
        collector?.Add("api", sw.ElapsedMilliseconds, $"{method} {path}", offsetMs);

        return response;
    }
}
