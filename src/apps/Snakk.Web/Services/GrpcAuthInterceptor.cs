using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Snakk.Protos.Auth;

namespace Snakk.Web.Services;

/// <summary>
/// Client-side gRPC interceptor that forwards JWT authentication from cookies to gRPC metadata.
/// Automatically refreshes expired access tokens using the refresh token cookie.
/// Uses request coalescing to prevent concurrent refresh races.
/// </summary>
public class GrpcAuthInterceptor : Interceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GrpcAuthInterceptor> _logger;

    // Lazy-initialized auth client for refresh calls (avoids circular dependency)
    private readonly Lazy<AuthService.AuthServiceClient> _authClientFactory;

    // Request coalescing: all concurrent requests sharing the same refresh token await the same task
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshOutcome>>> _refreshTasks = new();

    public GrpcAuthInterceptor(
        IHttpContextAccessor httpContextAccessor,
        Grpc.Net.Client.GrpcChannel channel,
        ILogger<GrpcAuthInterceptor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        // Create an unintercepted client for refresh calls to avoid recursion
        _authClientFactory = new Lazy<AuthService.AuthServiceClient>(
            () => new AuthService.AuthServiceClient(channel));
    }

    // Default deadline for all gRPC calls. Prevents hangs from stalled internal calls.
    // A call exceeding this returns RpcException with DeadlineExceeded, which callers handle gracefully.
    private static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(8);

    // Timing thresholds for gRPC call observability
    private static readonly TimeSpan SlowCallThreshold = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan VerySlowCallThreshold = TimeSpan.FromMilliseconds(2000);

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

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        var refreshToken = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];

        // Skip interception for RefreshToken calls to prevent recursion
        var methodName = context.Method?.Name;
        if (methodName == nameof(AuthService.AuthServiceClient.RefreshToken))
            return WrapWithTiming(continuation(request, context), context.Method!.FullName);

        // If access token is expired but we have a refresh token, auto-refresh
        if (!string.IsNullOrEmpty(refreshToken) && IsTokenExpired(accessToken))
        {
            // gRPC interceptors don't support async header modification, so we must block.
            // Bounded to 5 seconds to prevent thread pool starvation under load.
            // Request coalescing (below) ensures only one refresh runs per token.
            RefreshOutcome? outcome = null;
            try
            {
                outcome = RefreshTokensAsync(refreshToken, httpContext)
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();
            }
            catch (TimeoutException)
            {
                // API startup or overload — keep cookies, user stays anonymous for this request
                _logger.LogWarning("Token refresh timed out after 5 seconds in gRPC interceptor");
            }
            if (outcome?.Tokens is not null)
            {
                accessToken = outcome.Tokens.AccessToken;
                AuthCookieHelper.SetAuthCookies(httpContext, outcome.Tokens.AccessToken, outcome.Tokens.RefreshToken);
                _logger.LogDebug("Access token auto-refreshed via gRPC interceptor");
            }
            else if (outcome?.ShouldClearCookies == true)
            {
                // Server explicitly rejected the token (revoked/expired) — clear cookies so the
                // browser is not stuck in a loop with a permanently invalid token.
                _logger.LogWarning("Token explicitly rejected by server — clearing auth cookies");
                AuthCookieHelper.DeleteAuthCookies(httpContext);
            }
            // else: transient network/availability error — preserve cookies, user stays anonymous
            // for this request; next request will retry the refresh once the API is ready
        }

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

    /// <summary>
    /// Wraps an AsyncUnaryCall to log timing information when the response completes.
    /// Logs warnings for slow calls (>500ms) and errors for very slow calls (>2000ms).
    /// </summary>
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
        try
        {
            var result = await inner.ConfigureAwait(false);
            sw.Stop();
            LogTiming(methodFullName, sw.Elapsed, success: true);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogTiming(methodFullName, sw.Elapsed, success: false, error: ex.GetType().Name);
            throw;
        }
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

    private bool IsTokenExpired(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return true;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            return jwt.ValidTo <= DateTime.UtcNow.AddSeconds(30);
        }
        catch
        {
            return true;
        }
    }

    private async Task<RefreshOutcome> RefreshTokensAsync(string refreshToken, HttpContext httpContext)
    {
        var lazyTask = _refreshTasks.GetOrAdd(refreshToken, key => new Lazy<Task<RefreshOutcome>>(() =>
            ExecuteRefreshAsync(key)));

        try
        {
            return await lazyTask.Value;
        }
        finally
        {
            _refreshTasks.TryRemove(refreshToken, out _);
        }
    }

    private async Task<RefreshOutcome> ExecuteRefreshAsync(string refreshToken)
    {
        try
        {
            var authClient = _authClientFactory.Value;
            var response = await authClient.RefreshTokenAsync(new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            });

            if (!string.IsNullOrEmpty(response.AccessToken) && !string.IsNullOrEmpty(response.RefreshToken))
                return new RefreshOutcome(new RefreshTokens(response.AccessToken, response.RefreshToken), ShouldClearCookies: false);

            // Server responded but returned empty tokens — treat as explicit rejection
            return new RefreshOutcome(null, ShouldClearCookies: true);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.NotFound or StatusCode.PermissionDenied)
        {
            // Server explicitly rejected the token (revoked, expired, unknown)
            _logger.LogWarning("Token refresh rejected by server: {Status}", ex.Status);
            return new RefreshOutcome(null, ShouldClearCookies: true);
        }
        catch (RpcException ex)
        {
            // Transient error (Unavailable, DeadlineExceeded, Internal) — API may still be starting up
            _logger.LogWarning("gRPC token refresh unavailable (transient): {Status}", ex.Status);
            return new RefreshOutcome(null, ShouldClearCookies: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed unexpectedly");
            return new RefreshOutcome(null, ShouldClearCookies: false);
        }
    }

    private sealed record RefreshTokens(string AccessToken, string RefreshToken);
    private sealed record RefreshOutcome(RefreshTokens? Tokens, bool ShouldClearCookies);
}
