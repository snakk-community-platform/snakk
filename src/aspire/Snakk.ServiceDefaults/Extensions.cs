using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Snakk.ServiceDefaults;

public static class Extensions
{
    /// <summary>
    /// Adds Serilog structured logging only. Historically also intended to wire
    /// OTel + health checks, but was commented out across services in commit
    /// d14874b ("resolve all test failures") because the Serilog setup conflicted
    /// with test bootstrapping. Left in place for now; not currently called by any
    /// service. See <see cref="AddSnakkObservability"/> for the OTel + health
    /// path that IS wired.
    /// </summary>
    public static IHostApplicationBuilder AddSnakkDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureSerilog();
        return builder;
    }

    /// <summary>
    /// Adds OpenTelemetry traces/metrics/logs with OTLP export, plus default
    /// liveness/readiness health checks. Safe to call from any service (no
    /// Serilog wiring; no test interference). Pair with
    /// <see cref="MapDefaultEndpoints"/> after building the app.
    /// </summary>
    public static IHostApplicationBuilder AddSnakkObservability(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.ConfigurePyroscope();
        builder.AddDefaultHealthChecks();
        return builder;
    }

    /// <summary>
    /// Triggers the Pyroscope native profiler to load when
    /// <c>PYROSCOPE_SERVER_ADDRESS</c> is set in the process environment.
    /// Per-service application name comes from <c>PYROSCOPE_APPLICATION_NAME</c>,
    /// which is set per-program by supervisord in the docker container.
    /// The native side reads env vars at startup; we just need to ensure the
    /// managed wrapper assembly is loaded so its native side-effects fire.
    /// No-op when the env var is unset (unit tests, ad-hoc dev runs).
    /// </summary>
    private static IHostApplicationBuilder ConfigurePyroscope(this IHostApplicationBuilder builder)
    {
        var pyroscopeUrl = builder.Configuration["PYROSCOPE_SERVER_ADDRESS"];
        if (string.IsNullOrWhiteSpace(pyroscopeUrl))
            return builder;

        // Touching Pyroscope.Profiler.Instance forces the assembly to load,
        // which in turn p/invokes the native profiler that reads PYROSCOPE_*
        // env vars and starts pushing samples to the configured server.
        _ = Pyroscope.Profiler.Instance;
        return builder;
    }

    private static IHostApplicationBuilder ConfigureSerilog(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog(config =>
        {
            config
                .ReadFrom.Configuration(builder.Configuration)
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Routing", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Grpc", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName);

            if (builder.Environment.IsDevelopment())
            {
                config.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}");
            }
            else
            {
                config.WriteTo.Console(new Serilog.Formatting.Compact.RenderedCompactJsonFormatter());
            }
        });

        return builder;
    }

    /// <summary>
    /// Wires the .NET OpenTelemetry SDK: ASP.NET Core / HttpClient / gRPC / EF Core /
    /// Redis / Runtime / Process auto-instrumentations for traces and metrics, plus
    /// OTel logs. Exports OTLP when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set
    /// (Aspire sets this automatically in dev; docker-compose sets it to the
    /// Collector). No export if unset — services stay quiet in unit-test scenarios.
    /// </summary>
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: builder.Environment.ApplicationName,
                serviceInstanceId: Environment.MachineName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation())
            .WithTracing(tracing =>
            {
                // Dev: 100% sample. Prod: tail sampling lives in the Collector,
                // so services emit everything and let the Collector decide.
                if (builder.Environment.IsDevelopment())
                {
                    tracing.SetSampler(new AlwaysOnSampler());
                }

                tracing
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        // Skip health checks — they spam the trace store.
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                                       && !ctx.Request.Path.StartsWithSegments("/alive");
                    })
                    .AddHttpClientInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddRedisInstrumentation();
            });

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        // OTEL_EXPORTER_OTLP_ENDPOINT is the standard env var. Aspire sets it for
        // dev. Docker-compose sets it to http://otel-collector:4317.
        // If unset, fall back silently so unit tests and ad-hoc runs don't spam stderr.
        var endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // "self" — process is up and responding. Tagged "live" so /alive picks
            // it up; readiness checks (DB connectivity etc.) are added per service.
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps <c>/alive</c> (liveness — runs only checks tagged "live"; passes if
    /// the process can respond). Always exposed — liveness has no sensitive
    /// payload and is needed for container/K8s probes in every environment.
    /// Readiness endpoints (<c>/health</c>) are left to each service to own —
    /// they typically add DB/cache checks specific to that service.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
