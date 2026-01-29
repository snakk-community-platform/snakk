namespace Snakk.Web.Services;

/// <summary>
/// HTTP message handler that forwards authentication cookies from the incoming
/// browser request to outgoing API requests.
/// </summary>
public class CookieForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    // Cookie names that might be used by ASP.NET Core authentication
    private static readonly string[] AuthCookieNames =
    [
        ".AspNetCore.Cookies",           // Default for scheme "Cookies"
        ".AspNetCore.Application",       // Default application cookie
        ".AspNetCore.Identity.Application" // Identity cookie
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            // Collect all cookies to forward
            var cookiesToForward = new List<string>();

            // Forward known auth cookies
            foreach (var cookieName in AuthCookieNames)
            {
                if (httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieValue))
                {
                    cookiesToForward.Add($"{cookieName}={cookieValue}");
                }
            }

            // Also forward any cookies that look like auth cookies (start with .AspNetCore)
            foreach (var cookie in httpContext.Request.Cookies)
            {
                if (cookie.Key.StartsWith(".AspNetCore") && !AuthCookieNames.Contains(cookie.Key))
                {
                    cookiesToForward.Add($"{cookie.Key}={cookie.Value}");
                }
            }

            if (cookiesToForward.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", cookiesToForward));
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
