using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Grpc.Core;
using Prometheus;
using Snakk.Protos.Auth;

namespace Snakk.Web.Services;

/// <summary>
/// Proactively refreshes the access token when it is about to expire (within 2 minutes).
/// Because the old token is still valid at that point, the refresh runs in the background —
/// the current request is never blocked. New cookies are applied via OnStarting if the
/// refresh completes before the response headers flush. Uses request coalescing so only one
/// refresh call is made per token under concurrent load.
/// </summary>
public class TokenRefreshMiddleware(RequestDelegate next)
{
    public const string RefreshedAccessTokenKey = "RefreshedAccessToken";

    private static readonly JwtSecurityTokenHandler _jwtHandler = new();

    private static readonly Counter RefreshTotal = Metrics.CreateCounter(
        "snakk_token_refresh_total",
        "Token refresh outcomes by result",
        new CounterConfiguration { LabelNames = ["result"] });

    // Single-flight: concurrent requests sharing the same refresh token share one refresh call.
    // PublicationOnly avoids a Monitor lock — under a thundering herd a few duplicate gRPC calls
    // may fire, but token refresh is idempotent so that is acceptable.
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult>>> _inFlight = new();

    public async Task InvokeAsync(HttpContext context, AuthService.AuthServiceClient authClient)
    {
        var accessToken = context.Request.Cookies[AuthCookieHelper.AccessCookieName];
        var refreshToken = context.Request.Cookies[AuthCookieHelper.RefreshCookieName];

        if (!string.IsNullOrEmpty(refreshToken) && IsAboutToExpire(accessToken))
        {
            var lazyTask = _inFlight.GetOrAdd(refreshToken, key =>
                new Lazy<Task<RefreshResult>>(
                    () => ExecuteRefreshAsync(authClient, key),
                    LazyThreadSafetyMode.PublicationOnly));

            var refreshTask = lazyTask.Value;

            _ = refreshTask.ContinueWith(t => _inFlight.TryRemove(refreshToken, out _));

            // Apply new tokens if the refresh completes before response headers are flushed.
            // The current request proceeds immediately with the still-valid old token.
            context.Response.OnStarting(() =>
            {
                if (refreshTask.IsCompletedSuccessfully)
                {
                    var result = refreshTask.Result;
                    if (result.AccessToken is not null && result.RefreshToken is not null)
                    {
                        AuthCookieHelper.SetAuthCookies(context, result.AccessToken, result.RefreshToken);
                        RefreshTotal.WithLabels("success").Inc();
                    }
                    else if (result.ShouldClearCookies)
                    {
                        AuthCookieHelper.DeleteAuthCookies(context);
                        RefreshTotal.WithLabels("unauthenticated").Inc();
                    }
                    else
                    {
                        RefreshTotal.WithLabels("error").Inc();
                    }
                }
                else
                {
                    RefreshTotal.WithLabels("pending").Inc();
                }
                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    private static async Task<RefreshResult> ExecuteRefreshAsync(
        AuthService.AuthServiceClient authClient, string refreshToken)
    {
        try
        {
            var response = await authClient.RefreshTokenAsync(
                new RefreshTokenRequest { RefreshToken = refreshToken });

            if (!string.IsNullOrEmpty(response.AccessToken) && !string.IsNullOrEmpty(response.RefreshToken))
                return new RefreshResult(response.AccessToken, response.RefreshToken, ShouldClearCookies: false);

            return new RefreshResult(null, null, ShouldClearCookies: true);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated
                                           or StatusCode.NotFound
                                           or StatusCode.PermissionDenied)
        {
            return new RefreshResult(null, null, ShouldClearCookies: true);
        }
        catch
        {
            return new RefreshResult(null, null, ShouldClearCookies: false);
        }
    }

    private static bool IsAboutToExpire(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var jwt = _jwtHandler.ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow.AddMinutes(2);
        }
        catch
        {
            return false;
        }
    }

    private sealed record RefreshResult(string? AccessToken, string? RefreshToken, bool ShouldClearCookies);
}
