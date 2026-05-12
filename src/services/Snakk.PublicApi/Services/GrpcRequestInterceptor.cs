namespace Snakk.PublicApi.Services;

using Grpc.Core;
using Grpc.Core.Interceptors;

public class GrpcRequestInterceptor(
    IHttpContextAccessor httpContextAccessor,
    ILogger<GrpcRequestInterceptor> logger) : Interceptor
{
    private static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WarnThreshold = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ErrorThreshold = TimeSpan.FromMilliseconds(2000);

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = new Metadata();

        // Forward Bearer token if present (for future authenticated endpoints)
        var httpContext = httpContextAccessor.HttpContext;
        var authorization = httpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
            headers.Add("authorization", authorization);

        // Apply default deadline if none set
        var options = context.Options;
        if (options.Deadline is null)
            options = options.WithDeadline(DateTime.UtcNow.Add(DefaultDeadline));

        var updated = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, options.WithHeaders(headers));

        var start = DateTime.UtcNow;
        var call = continuation(request, updated);

        return new AsyncUnaryCall<TResponse>(
            WrapWithTiming(call.ResponseAsync, context.Method.Name, start),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> WrapWithTiming<TResponse>(
        Task<TResponse> task, string method, DateTime start)
    {
        var response = await task;
        var elapsed = DateTime.UtcNow - start;

        if (elapsed >= ErrorThreshold)
            logger.LogError("Slow gRPC call {Method}: {ElapsedMs}ms", method, (int)elapsed.TotalMilliseconds);
        else if (elapsed >= WarnThreshold)
            logger.LogWarning("Slow gRPC call {Method}: {ElapsedMs}ms", method, (int)elapsed.TotalMilliseconds);

        return response;
    }
}
