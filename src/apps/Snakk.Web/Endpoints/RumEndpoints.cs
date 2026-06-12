namespace Snakk.Web.Endpoints;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;

/// <summary>
/// Real User Monitoring (RUM) endpoint.
/// Accepts Web Vitals beacons from the client-side web-vitals.ts collector.
/// Logs one structured line per beacon; always returns 204.
/// </summary>
public static class RumEndpoints
{
    private const int MaxBodyBytes = 2048;
    private const int MaxPathLength = 512;

    // Reasonable clamp ranges for web vitals
    private const double MaxLcpMs  = 60_000;   // 60 s
    private const double MaxClsVal =  10.0;    // pathological page
    private const double MaxInpMs  = 60_000;   // 60 s
    private const double MaxTtfbMs = 60_000;   // 60 s

    public static void MapRumEndpoints(this IEndpointRouteBuilder app)
    {
        // Cast to Delegate: with an HttpContext-only signature the method otherwise
        // binds as a RequestDelegate and the returned IResult (204) is discarded.
        app.MapPost("/bff/rum", (Delegate)HandleRumAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private static async Task<IResult> HandleRumAsync(HttpContext context)
    {
        // Read body manually: sendBeacon may send content-type text/plain,
        // which the [FromBody] binding won't deserialise.
        string body;
        try
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(
                context.Request.Body,
                leaveOpen: true,
                encoding: System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: MaxBodyBytes + 1);

            // Read at most MaxBodyBytes + 1 so we can detect oversized payloads cheaply
            var buffer = new char[MaxBodyBytes + 1];
            int read = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (read > MaxBodyBytes)
                return Results.NoContent(); // oversized — silently drop

            body = new string(buffer, 0, read);
        }
        catch
        {
            return Results.NoContent();
        }

        // Tolerate parse failures — field is optional telemetry
        RumPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RumPayload>(body, JsonOpts);
        }
        catch
        {
            return Results.NoContent();
        }

        if (payload is null)
            return Results.NoContent();

        // Validate and clamp
        var path = payload.Path ?? "";
        if (path.Length > MaxPathLength)
            path = path[..MaxPathLength];

        var metrics = payload.Metrics;
        double? lcp  = ClampPositive(metrics?.Lcp,  MaxLcpMs);
        double? cls  = ClampPositive(metrics?.Cls,  MaxClsVal);
        double? inp  = ClampPositive(metrics?.Inp,  MaxInpMs);
        double? ttfb = ClampPositive(metrics?.Ttfb, MaxTtfbMs);

        // Log one structured line; the category "Snakk.WebVitals" can be filtered
        // independently in appsettings.json (e.g. route to a separate sink).
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Snakk.WebVitals");

        logger.LogInformation(
            "RUM lcp={Lcp} cls={Cls} inp={Inp} ttfb={Ttfb} path={Path} effectiveType={EffectiveType} htmxNav={HtmxNav}",
            lcp, cls, inp, ttfb,
            path,
            payload.EffectiveType,
            payload.HtmxNav);

        return Results.NoContent();
    }

    private static double? ClampPositive(double? value, double max)
    {
        if (value is null) return null;
        if (value < 0)   return 0;
        if (value > max) return max;
        return value;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record RumPayload(
        [property: JsonPropertyName("metrics")]      RumMetrics? Metrics,
        [property: JsonPropertyName("path")]         string?     Path,
        [property: JsonPropertyName("effectiveType")]string?     EffectiveType,
        [property: JsonPropertyName("htmxNav")]      bool        HtmxNav);

    private sealed record RumMetrics(
        [property: JsonPropertyName("lcp")]  double? Lcp,
        [property: JsonPropertyName("cls")]  double? Cls,
        [property: JsonPropertyName("inp")]  double? Inp,
        [property: JsonPropertyName("ttfb")] double? Ttfb);
}
