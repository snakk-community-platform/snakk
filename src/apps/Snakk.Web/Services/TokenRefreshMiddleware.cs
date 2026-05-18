using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Web.Services;

/// <summary>
/// Runs before UseAuthentication(). If the access token cookie is expired but a valid
/// refresh token cookie exists, silently exchanges it for new tokens. The refreshed tokens
/// are stored in HttpContext.Items so GrpcAuthInterceptor and OnMessageReceived can use
/// them without triggering a second (invalid) refresh with the now-rotated refresh token.
/// Uses request coalescing so only one refresh call is made per token under concurrent load.
/// </summary>
public class TokenRefreshMiddleware(RequestDelegate next)
{
    public const string RefreshedAccessTokenKey = "RefreshedAccessToken";
    public const string RefreshedRefreshTokenKey = "RefreshedRefreshToken";

    // Single-flight: concurrent requests sharing the same refresh token share one refresh call
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult>>> _inFlight = new();

    public async Task InvokeAsync(HttpContext context, AuthService.AuthServiceClient authClient)
    {
        var accessToken = context.Request.Cookies[AuthCookieHelper.AccessCookieName];
        var refreshToken = context.Request.Cookies[AuthCookieHelper.RefreshCookieName];

        if (!string.IsNullOrEmpty(refreshToken) && IsExpired(accessToken))
        {
            RefreshResult? result = null;
            try
            {
                var lazyTask = _inFlight.GetOrAdd(refreshToken, key =>
                    new Lazy<Task<RefreshResult>>(() => ExecuteRefreshAsync(authClient, key)));

                try
                {
                    result = await lazyTask.Value.WaitAsync(TimeSpan.FromSeconds(15));
                }
                finally
                {
                    _inFlight.TryRemove(refreshToken, out _);
                }
            }
            catch (TimeoutException)
            {
                // API startup or overload — proceed anonymously, cookies intact for next request
            }
            catch
            {
                // Swallow unexpected failures — proceed anonymously
            }

            if (result?.AccessToken is not null && result.RefreshToken is not null)
            {
                context.Items[RefreshedAccessTokenKey] = result.AccessToken;
                context.Items[RefreshedRefreshTokenKey] = result.RefreshToken;
                AuthCookieHelper.SetAuthCookies(context, result.AccessToken, result.RefreshToken);
            }
            else if (result?.ShouldClearCookies == true)
            {
                AuthCookieHelper.DeleteAuthCookies(context);
            }
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
            // Transient failure — preserve cookies so next request can retry
            return new RefreshResult(null, null, ShouldClearCookies: false);
        }
    }

    private static bool IsExpired(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private sealed record RefreshResult(string? AccessToken, string? RefreshToken, bool ShouldClearCookies);
}
