using Grpc.Net.Client;
using System.Diagnostics;

namespace Snakk.Web.Services;

/// <summary>
/// Eagerly establishes the gRPC HTTP/2 connection to the API at startup.
/// Without this, the channel starts in IDLE state. The first failed connection attempt
/// (e.g. if the API isn't ready yet) puts the channel into TRANSIENT_FAILURE with
/// exponential backoff — causing the first batch of user requests to queue up and wait
/// up to ~20 seconds for the backoff timer to fire and the retry to succeed.
/// </summary>
public class GrpcChannelWarmupService(
    GrpcChannel channel,
    ILogger<GrpcChannelWarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
