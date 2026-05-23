using Grpc.Net.Client;
using Prometheus;
using System.Diagnostics;

namespace Snakk.Web.Services;

/// <summary>
/// Eagerly establishes the gRPC HTTP/2 connection to the API at startup.
/// Without this, the channel starts in IDLE state. The first failed connection attempt
/// (e.g. if the API isn't ready yet) puts the channel into TRANSIENT_FAILURE with
/// exponential backoff — causing the first batch of user requests to queue up and wait
/// up to ~20 seconds for the backoff timer to fire and the retry to succeed.
/// Also tracks channel connectivity state as a Prometheus gauge for Grafana visibility.
/// </summary>
public class GrpcChannelWarmupService(
    GrpcChannel channel,
    ILogger<GrpcChannelWarmupService> logger) : IHostedService
{
    // 0=Idle, 1=Connecting, 2=Ready, 3=TransientFailure, 4=Shutdown
    private static readonly Gauge ChannelState = Metrics.CreateGauge(
        "snakk_grpc_channel_state",
        "gRPC channel connectivity state (0=Idle 1=Connecting 2=Ready 3=TransientFailure 4=Shutdown)");

    private CancellationTokenSource? _cts;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        ChannelState.Set((double)channel.State);
        logger.LogInformation("gRPC warmup: connecting to API (state: {State})", channel.State);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            await channel.ConnectAsync(cts.Token);
            logger.LogInformation("gRPC warmup: connected in {Ms}ms", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("gRPC warmup: timed out after {Ms}ms — first user request may be slow", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "gRPC warmup: failed after {Ms}ms — will retry on first request", sw.ElapsedMilliseconds);
        }

        _cts = new CancellationTokenSource();
        _ = TrackStateAsync(_cts.Token);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task TrackStateAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var state = channel.State;
            ChannelState.Set((double)state);

            if (state != Grpc.Core.ConnectivityState.Ready)
                logger.LogWarning("gRPC channel state changed to {State}", state);

            try
            {
                await channel.WaitForStateChangedAsync(state, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
