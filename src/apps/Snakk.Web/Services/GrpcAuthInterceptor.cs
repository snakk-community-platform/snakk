using Grpc.Core;
using Grpc.Core.Interceptors;
using Prometheus;

namespace Snakk.Web.Services;

/// <summary>
/// Client-side gRPC interceptor that forwards JWT authentication from cookies to gRPC metadata.
/// Catches USER_REVOKED responses, clears auth cookies, and throws AuthenticationRevokedException
/// so the request pipeline can redirect to /auth/login.
/// </summary>
public class GrpcAuthInterceptor : Interceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GrpcAuthInterceptor> _logger;

    private static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan SlowCallThreshold = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan VerySlowCallThreshold = TimeSpan.FromMilliseconds(2000);

    private static readonly Histogram CallDuration = Metrics.CreateHistogram(
        "snakk_grpc_client_duration_seconds",
        "gRPC client call duration by method and status",
        new HistogramConfiguration
        {
            LabelNames = ["method", "status"],
            Buckets = [.005, .01, .025, .05, .1, .25, .5, 1, 2, 5, 8]
        });

    public GrpcAuthInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ILogger<GrpcAuthInterceptor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        // Apply default deadline if caller didn't specify one
        if (context.Options.Deadline is null || context.Options.Deadline == DateTime.MaxValue)
        {
            var withDeadline = context.Options.WithDeadline(DateTime.UtcNow.Add(DefaultDeadline));
            context = new ClientInterceptorContext<TRequest, TResponse>(context.Method!, context.Host, withDeadline);
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return WrapWithTiming(continuation(request, context), context.Method!.FullName);

        // Prefer the token refreshed this request (from TokenRefreshMiddleware) over the raw cookie
        var accessToken = httpContext.Items[TokenRefreshMiddleware.RefreshedAccessTokenKey] as string
            ?? httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];

        if (!string.IsNullOrEmpty(accessToken))
        {
            var headers = context.Options.Headers ?? new Metadata();
            headers.Add("authorization", $"Bearer {accessToken}");

            var newOptions = context.Options.WithHeaders(headers);
            var newContext = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method!, context.Host, newOptions);

            return WrapWithTiming(continuation(request, newContext), context.Method!.FullName);
        }

        return WrapWithTiming(continuation(request, context), context.Method!.FullName);
    }

    private AsyncUnaryCall<TResponse> WrapWithTiming<TResponse>(
        AsyncUnaryCall<TResponse> call, string methodFullName)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timedResponse = WrapResponseAsync(call.ResponseAsync, sw, methodFullName);

        return new AsyncUnaryCall<TResponse>(
            timedResponse,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> WrapResponseAsync<TResponse>(
        Task<TResponse> inner, System.Diagnostics.Stopwatch sw, string methodFullName)
    {
        var method = ShortMethodName(methodFullName);
        try
        {
            var result = await inner.ConfigureAwait(false);
            sw.Stop();
            CallDuration.WithLabels(method, "OK").Observe(sw.Elapsed.TotalSeconds);
            LogTiming(methodFullName, sw.Elapsed, success: true);
            return result;
        }
        catch (RpcException ex) when (
            ex.StatusCode == StatusCode.Unauthenticated &&
            ex.Status.Detail == "USER_REVOKED")
        {
            sw.Stop();
            CallDuration.WithLabels(method, "Unauthenticated").Observe(sw.Elapsed.TotalSeconds);
            if (_httpContextAccessor.HttpContext is { } ctx)
                AuthCookieHelper.DeleteAuthCookies(ctx);
            throw new AuthenticationRevokedException();
        }
        catch (RpcException ex)
        {
            sw.Stop();
            CallDuration.WithLabels(method, ex.StatusCode.ToString()).Observe(sw.Elapsed.TotalSeconds);
            LogTiming(methodFullName, sw.Elapsed, success: false, error: ex.GetType().Name);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            CallDuration.WithLabels(method, "Error").Observe(sw.Elapsed.TotalSeconds);
            LogTiming(methodFullName, sw.Elapsed, success: false, error: ex.GetType().Name);
            throw;
        }
    }

    // "/snakk.auth.v1.AuthService/RefreshToken" → "AuthService/RefreshToken"
    private static string ShortMethodName(string fullName)
    {
        var parts = fullName.TrimStart('/').Split('/');
        return parts.Length >= 2
            ? $"{parts[^2].Split('.')[^1]}/{parts[^1]}"
            : fullName;
    }

    private void LogTiming(string methodFullName, TimeSpan elapsed, bool success, string? error = null)
    {
        var ms = (int)elapsed.TotalMilliseconds;

        if (elapsed >= VerySlowCallThreshold)
        {
            _logger.LogError(
                "Very slow gRPC call: {Method} took {DurationMs}ms (success={Success}{Error})",
                methodFullName, ms, success, error is null ? "" : $", error={error}");
        }
        else if (elapsed >= SlowCallThreshold)
        {
            _logger.LogWarning(
                "Slow gRPC call: {Method} took {DurationMs}ms (success={Success}{Error})",
                methodFullName, ms, success, error is null ? "" : $", error={error}");
        }
    }
}
