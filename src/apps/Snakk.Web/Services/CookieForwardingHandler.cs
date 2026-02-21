namespace Snakk.Web.Services;

/// <summary>
/// HTTP message handler that forwards JWT authentication from SSO cookie to outgoing API requests.
/// Reads the ".Snakk.Auth" cookie and adds it as a Bearer token in the Authorization header.
/// </summary>
public class CookieForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private const string AuthCookieName = ".Snakk.Auth";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            // Read JWT from SSO cookie
            var token = httpContext.Request.Cookies[AuthCookieName];

            if (!string.IsNullOrEmpty(token))
            {
                // Add JWT as Bearer token to Authorization header
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
