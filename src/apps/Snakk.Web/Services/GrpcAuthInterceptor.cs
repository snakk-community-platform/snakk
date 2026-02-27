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
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult?>>> _refreshTasks = new();

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

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return continuation(request, context);

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        var refreshToken = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];

        // Skip interception for RefreshToken calls to prevent recursion
        var methodName = context.Method?.Name;
        if (methodName == nameof(AuthService.AuthServiceClient.RefreshToken))
            return continuation(request, context);

        // If access token is expired but we have a refresh token, auto-refresh
        if (!string.IsNullOrEmpty(refreshToken) && IsTokenExpired(accessToken))
        {
            // We need to refresh synchronously-ish here. Use blocking wait since gRPC
            // interceptors don't have a clean async pattern for modifying headers.
            var refreshResult = RefreshTokensAsync(refreshToken, httpContext).GetAwaiter().GetResult();
            if (refreshResult != null)
            {
                accessToken = refreshResult.AccessToken;
                AuthCookieHelper.SetAuthCookies(httpContext, refreshResult.AccessToken, refreshResult.RefreshToken);
                _logger.LogDebug("Access token auto-refreshed via gRPC interceptor");
            }
            else
            {
                _logger.LogWarning("Token auto-refresh failed in gRPC interceptor");
            }
        }

        if (!string.IsNullOrEmpty(accessToken))
        {
            var headers = context.Options.Headers ?? new Metadata();
            headers.Add("authorization", $"Bearer {accessToken}");

            var newOptions = context.Options.WithHeaders(headers);
            var newContext = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method, context.Host, newOptions);

            return continuation(request, newContext);
        }

        return continuation(request, context);
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

    private async Task<RefreshResult?> RefreshTokensAsync(string refreshToken, HttpContext httpContext)
    {
        var lazyTask = _refreshTasks.GetOrAdd(refreshToken, key => new Lazy<Task<RefreshResult?>>(() =>
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

    private async Task<RefreshResult?> ExecuteRefreshAsync(string refreshToken)
    {
        try
        {
            var authClient = _authClientFactory.Value;
            var response = await authClient.RefreshTokenAsync(new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            });

            if (!string.IsNullOrEmpty(response.AccessToken) && !string.IsNullOrEmpty(response.RefreshToken))
            {
                return new RefreshResult(response.AccessToken, response.RefreshToken);
            }

            return null;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning("gRPC token refresh failed: {Status}", ex.Status);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed with exception");
            return null;
        }
    }

    private sealed record RefreshResult(string AccessToken, string RefreshToken);
}
