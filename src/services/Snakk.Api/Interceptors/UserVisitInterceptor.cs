namespace Snakk.Api.Interceptors;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Snakk.Api.Services;
using Snakk.Application.Services;

/// <summary>
/// Tracks per-user visit boundaries to support "new since last visit" unread counts.
/// Delegates to IUserVisitTracker (HybridCache-guarded, ~1 DB write per 30 min per user).
/// </summary>
public class UserVisitInterceptor(
    IUserVisitTracker visitTracker,
    ICurrentUserService currentUser) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await TrackAsync(context.CancellationToken);
        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await TrackAsync(context.CancellationToken);
        return await continuation(requestStream, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await TrackAsync(context.CancellationToken);
        await continuation(request, responseStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await TrackAsync(context.CancellationToken);
        await continuation(requestStream, responseStream, context);
    }

    private async Task TrackAsync(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated()) return;
        var userId = currentUser.GetCurrentUserId();
        if (userId is null) return;
        await visitTracker.TrackVisitAsync(userId, ct);
    }
}
