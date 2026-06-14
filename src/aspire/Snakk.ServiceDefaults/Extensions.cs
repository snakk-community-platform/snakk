using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
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
        var options = builder.Configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        // Bind the live options into DI too, so app code (e.g. the RUM endpoint
        // mapping and the layout's beacon <script>) can read the same flags.
        builder.Services.Configure<ObservabilityOptions>(
            builder.Configuration.GetSection(ObservabilityOptions.SectionName));

        if (options.Enabled)
        {
            builder.ConfigureOpenTelemetry(options);
            builder.ConfigurePyroscope(options);
        }

        // Health checks are independent of telemetry flags — liveness/readiness
        // probes must work even with all observability shed.
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
    private static IHostApplicationBuilder ConfigurePyroscope(this IHostApplicationBuilder builder, ObservabilityOptions options)
    {
        // Profiling is the single biggest per-process telemetry cost, so it has
        // its own kill switch on top of the env-var gate. Opt it OUT in prod
        // appsettings (Observability:Profiling:Enabled=false) until a profile is
        // actually needed; the env var alone no longer turns it on.
        if (!options.IsOn(options.Profiling))
            return builder;

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
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder, ObservabilityOptions options)
    {
        // OTel logs — only register the provider when the signal is on, so the
        // IncludeScopes/IncludeFormattedMessage capture cost isn't paid otherwise.
        if (options.IsOn(options.OtlpLogs))
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });
        }

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: builder.Environment.ApplicationName,
                serviceInstanceId: Environment.MachineName));

        if (options.IsOn(options.Metrics))
        {
            otel.WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                // SignalR/WebSocket saturation (Snakk.Realtime): hub + long-running
                // connection counts/durations. Only Realtime emits these; harmless
                // elsewhere. Npgsql pool, gRPC client/server (rpc_*), and Kestrel
                // connection metrics already flow via the instrumentations above.
                .AddMeter("Microsoft.AspNetCore.Http.Connections")
                .AddMeter("Microsoft.AspNetCore.SignalR.Server"));
        }

        if (options.IsOn(options.Tracing))
        {
            otel.WithTracing(tracing =>
            {
                // Dev: always 100% so local traces are complete. Prod: head-sample
                // at the configured ratio (default 1.0 = prior behaviour) BEFORE
                // the Collector's tail sampler, so trace volume can be shed at the
                // source under load. ParentBased keeps a trace's spans together.
                tracing.SetSampler(builder.Environment.IsDevelopment()
                    ? new AlwaysOnSampler()
                    : new ParentBasedSampler(new TraceIdRatioBasedSampler(options.Tracing.SamplingRatio)));

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
        }

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
