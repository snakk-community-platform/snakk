using Microsoft.Extensions.Configuration;
using Snakk.ServiceDefaults;

namespace Snakk.Web.Tests.Services;

/// <summary>
/// Contract for the observability feature flags. The load-bearing invariant is
/// back-compat: a service with NO <c>Observability</c> section must behave
/// exactly as before the flags existed (everything on, full sampling).
/// </summary>
public class ObservabilityOptionsTests
{
    private static ObservabilityOptions Bind(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return config.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();
    }

    [Test]
    public async Task AbsentSection_DefaultsToFullyOn_BackCompat()
    {
        var o = Bind(new Dictionary<string, string?>());

        await Assert.That(o.Enabled).IsTrue();
        await Assert.That(o.IsOn(o.Metrics)).IsTrue();
        await Assert.That(o.IsOn(o.Tracing)).IsTrue();
        await Assert.That(o.IsOn(o.OtlpLogs)).IsTrue();
        await Assert.That(o.IsOn(o.Profiling)).IsTrue();
        await Assert.That(o.IsOn(o.Rum)).IsTrue();
        await Assert.That(o.Tracing.SamplingRatio).IsEqualTo(1.0);
    }

    [Test]
    public async Task MasterDisabled_TurnsOffEverySignal_RegardlessOfChildFlags()
    {
        var o = Bind(new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = "false",
            ["Observability:Metrics:Enabled"] = "true",
            ["Observability:Tracing:Enabled"] = "true",
        });

        await Assert.That(o.IsOn(o.Metrics)).IsFalse();
        await Assert.That(o.IsOn(o.Tracing)).IsFalse();
        await Assert.That(o.IsOn(o.Rum)).IsFalse();
    }

    [Test]
    public async Task IndividualSignal_CanBeShedWhileOthersStayOn()
    {
        var o = Bind(new Dictionary<string, string?>
        {
            ["Observability:Profiling:Enabled"] = "false",
            ["Observability:Rum:Enabled"] = "false",
            ["Observability:Tracing:SamplingRatio"] = "0.1",
        });

        await Assert.That(o.IsOn(o.Profiling)).IsFalse();
        await Assert.That(o.IsOn(o.Rum)).IsFalse();
        await Assert.That(o.IsOn(o.Metrics)).IsTrue();
        await Assert.That(o.IsOn(o.Tracing)).IsTrue();
        await Assert.That(o.Tracing.SamplingRatio).IsEqualTo(0.1);
    }
}
