using System.IdentityModel.Tokens.Jwt;
using Grpc.Core;
using Snakk.Protos.Auth;

namespace Snakk.Web.Services;

/// <summary>
/// Runs before UseAuthentication(). If the access token cookie is expired but a valid
/// refresh token cookie exists, silently exchanges it for new tokens. The refreshed tokens
/// are stored in HttpContext.Items so GrpcAuthInterceptor and OnMessageReceived can use
/// them without triggering a second (invalid) refresh with the now-rotated refresh token.
/// </summary>
public class TokenRefreshMiddleware(RequestDelegate next)
{
    public const string RefreshedAccessTokenKey = "RefreshedAccessToken";
    public const string RefreshedRefreshTokenKey = "RefreshedRefreshToken";

    public async Task InvokeAsync(HttpContext context, AuthService.AuthServiceClient authClient)
    {
        var accessToken = context.Request.Cookies[AuthCookieHelper.AccessCookieName];
        var refreshToken = context.Request.Cookies[AuthCookieHelper.RefreshCookieName];

        if (!string.IsNullOrEmpty(refreshToken) && IsExpired(accessToken))
        {
            try
            {
                var response = await authClient.RefreshTokenAsync(
                    new RefreshTokenRequest { RefreshToken = refreshToken });

                if (!string.IsNullOrEmpty(response.AccessToken) && !string.IsNullOrEmpty(response.RefreshToken))
                {
                    context.Items[RefreshedAccessTokenKey] = response.AccessToken;
                    context.Items[RefreshedRefreshTokenKey] = response.RefreshToken;
                    AuthCookieHelper.SetAuthCookies(context, response.AccessToken, response.RefreshToken);
                }
                else
                {
                    AuthCookieHelper.DeleteAuthCookies(context);
                }
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated
                                              or StatusCode.NotFound
                                              or StatusCode.PermissionDenied)
            {
                // Server explicitly rejected the refresh token — clear cookies
                AuthCookieHelper.DeleteAuthCookies(context);
            }
            catch
            {
                // Transient failure (API restart, timeout) — proceed anonymously this request,
                // cookies remain intact so the next request can retry
            }
        }

        await next(context);
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
}
