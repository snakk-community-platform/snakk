using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Snakk.Protos.Auth;

namespace Snakk.Admin.Services;

/// <summary>
/// Client-side gRPC interceptor for Blazor Server circuits.
/// Reads JWT from CircuitTokenProvider (not HttpContext cookies, which are unavailable after initial request).
/// Automatically refreshes expired access tokens using the stored refresh token.
/// </summary>
public class GrpcAuthInterceptor : Interceptor
{
    private readonly CircuitTokenProvider _tokenProvider;
    private readonly ILogger<GrpcAuthInterceptor> _logger;
    private readonly Lazy<AuthService.AuthServiceClient> _authClientFactory;

    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult?>>> _refreshTasks = new();

    public GrpcAuthInterceptor(
        CircuitTokenProvider tokenProvider,
        Grpc.Net.Client.GrpcChannel channel,
        ILogger<GrpcAuthInterceptor> logger)
    {
        _tokenProvider = tokenProvider;
        _logger = logger;
        _authClientFactory = new Lazy<AuthService.AuthServiceClient>(
            () => new AuthService.AuthServiceClient(channel));
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var accessToken = _tokenProvider.Token;
        var refreshToken = _tokenProvider.RefreshToken;

        _logger.LogDebug("gRPC interceptor: method={Method}, hasToken={HasToken}, hasRefresh={HasRefresh}",
            context.Method?.Name, !string.IsNullOrEmpty(accessToken), !string.IsNullOrEmpty(refreshToken));

        // Skip interception for RefreshToken calls to prevent recursion
        var methodName = context.Method?.Name;
        if (methodName == nameof(AuthService.AuthServiceClient.RefreshToken))
            return continuation(request, context);

        // If access token is expired but we have a refresh token, auto-refresh
        if (!string.IsNullOrEmpty(refreshToken) && IsTokenExpired(accessToken))
        {
            _logger.LogInformation("Access token expired, attempting refresh");
            var refreshResult = RefreshTokensAsync(refreshToken).ConfigureAwait(false).GetAwaiter().GetResult();
            if (refreshResult is not null)
            {
                _tokenProvider.Token = refreshResult.AccessToken;
                _tokenProvider.RefreshToken = refreshResult.RefreshToken;
                accessToken = refreshResult.AccessToken;
                _logger.LogDebug("Access token auto-refreshed via gRPC interceptor");
            }
            else
                _logger.LogWarning("Token auto-refresh failed in gRPC interceptor");
        }

        if (!string.IsNullOrEmpty(accessToken))
        {
            var headers = context.Options.Headers ?? new Metadata();
            headers.Add("authorization", $"Bearer {accessToken}");

            var newOptions = context.Options.WithHeaders(headers);
            var newContext = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method!, context.Host, newOptions);

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

    private async Task<RefreshResult?> RefreshTokensAsync(string refreshToken)
    {
        var lazyTask = _refreshTasks.GetOrAdd(refreshToken, key => new Lazy<Task<RefreshResult?>>(() =>
            ExecuteRefreshAsync(key)));

        try
        {
            return await lazyTask.Value.ConfigureAwait(false);
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
                return new RefreshResult(response.AccessToken, response.RefreshToken);

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
