namespace Snakk.ServiceDefaults;

/// <summary>
/// Production feature flags for the observability stack, bound from the
/// <c>Observability</c> configuration section. Each telemetry signal is an
/// independent kill switch so its cost can be shed in prod without a code
/// change (env-overridable, e.g. <c>Observability__Tracing__Enabled=false</c>).
///
/// IMPORTANT — backward compatibility: every default here preserves the
/// pre-flag behaviour, so a service with NO <c>Observability</c> section behaves
/// exactly as it did before this type existed. The flags are an *additional*
/// gate layered on top of the existing env-var gates: OTLP export still also
/// requires <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, and profiling still also
/// requires <c>PYROSCOPE_SERVER_ADDRESS</c>. A flag set to <c>false</c> disables
/// its signal even when the corresponding env var is present.
///
/// Pipelines and the Pyroscope native agent read configuration once at process
/// start, so flag changes take effect on restart — see
/// docs/OBSERVABILITY-OPS-DOS-AND-DONTS.md for the per-flag cost/restart notes.
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Master kill switch. When false, NO OTel pipeline (traces,
    /// metrics, logs) and no profiler are registered — not merely export-gated,
    /// so the per-request instrumentation cost (Activity creation, the EF Core
    /// interceptor, scope capture) is skipped entirely.</summary>
    public bool Enabled { get; set; } = true;

    public TracingOptions Tracing { get; set; } = new();
    public SignalToggle Metrics { get; set; } = new();
    public SignalToggle OtlpLogs { get; set; } = new();
    public SignalToggle Profiling { get; set; } = new();
    public SignalToggle Rum { get; set; } = new();

    /// <summary>True when the master switch and the given signal are both on.</summary>
    public bool IsOn(SignalToggle signal) => Enabled && signal.Enabled;
}

/// <summary>A single on/off telemetry signal. Defaults to on for back-compat.</summary>
public class SignalToggle
{
    public bool Enabled { get; set; } = true;
}

public sealed class TracingOptions : SignalToggle
{
    /// <summary>Head-sampling probability [0.0–1.0] applied in non-Development
    /// environments via <c>ParentBased(TraceIdRatioBased(ratio))</c>. Default
    /// 1.0 preserves the prior "emit every span" behaviour; lower it in prod
    /// appsettings to shed trace volume before the Collector's tail sampler even
    /// sees it. Development always uses <c>AlwaysOnSampler</c> regardless.</summary>
    public double SamplingRatio { get; set; } = 1.0;
}
