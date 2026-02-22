using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;

namespace Snakk.Web.Services;

/// <summary>
/// HTTP message handler that forwards JWT authentication from cookies to outgoing API requests.
/// Automatically refreshes expired access tokens using the refresh token cookie.
/// Uses request coalescing to prevent concurrent refresh races.
/// </summary>
public class CookieForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CookieForwardingHandler> _logger;

    // Request coalescing: all concurrent requests sharing the same refresh token await the same task
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult?>>> _refreshTasks = new();

    public CookieForwardingHandler(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CookieForwardingHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
            var refreshToken = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];

            // If access token is expired but we have a refresh token, auto-refresh
            if (!string.IsNullOrEmpty(refreshToken) && IsTokenExpired(accessToken))
            {
                var refreshResult = await RefreshTokensAsync(refreshToken, cancellationToken);
                if (refreshResult != null)
                {
                    accessToken = refreshResult.AccessToken;
                    // Set updated cookies on the response
                    AuthCookieHelper.SetAuthCookies(httpContext, refreshResult.AccessToken, refreshResult.RefreshToken);
                    _logger.LogDebug("Access token auto-refreshed via CookieForwardingHandler");
                }
                else
                {
                    _logger.LogWarning("Token auto-refresh failed, proceeding with expired token");
                }
            }

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private bool IsTokenExpired(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return true;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            // Consider expired if less than 30 seconds remaining (buffer for network latency)
            return jwt.ValidTo <= DateTime.UtcNow.AddSeconds(30);
        }
        catch
        {
            return true;
        }
    }

    private async Task<RefreshResult?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken)
    {
        // Request coalescing: if another request is already refreshing with this token, await it
        var lazyTask = _refreshTasks.GetOrAdd(refreshToken, key => new Lazy<Task<RefreshResult?>>(() =>
            ExecuteRefreshAsync(key, cancellationToken)));

        try
        {
            return await lazyTask.Value;
        }
        finally
        {
            _refreshTasks.TryRemove(refreshToken, out _);
        }
    }

    private async Task<RefreshResult?> ExecuteRefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:5242";
            var httpClient = _httpClientFactory.CreateClient();

            var requestBody = new { refreshToken };
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync($"{apiBaseUrl}/auth/refresh", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token refresh API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

            if (result.TryGetProperty("accessToken", out var accessTokenEl) &&
                result.TryGetProperty("refreshToken", out var refreshTokenEl))
            {
                var newAccessToken = accessTokenEl.GetString();
                var newRefreshToken = refreshTokenEl.GetString();

                if (!string.IsNullOrEmpty(newAccessToken) && !string.IsNullOrEmpty(newRefreshToken))
                {
                    return new RefreshResult(newAccessToken, newRefreshToken);
                }
            }

            _logger.LogWarning("Token refresh response missing expected token fields");
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
