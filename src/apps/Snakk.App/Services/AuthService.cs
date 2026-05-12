namespace Snakk.App.Services;

using IdentityModel.OidcClient;

public class AuthService
{
    private const string AccessTokenKey = "snakk.access_token";
    private const string RefreshTokenKey = "snakk.refresh_token";
    private const string ExpiryKey = "snakk.token_expiry";

    private readonly OidcClient _oidcClient;
    private bool _isLoggedIn;

    public bool IsLoggedIn => _isLoggedIn;

    public AuthService(string apiBaseUrl)
    {
        _oidcClient = new OidcClient(new OidcClientOptions
        {
            Authority = $"{apiBaseUrl}/auth",
            ClientId = "snakk-maui",
            Scope = "openid offline_access discussions:read",
            RedirectUri = "http://127.0.0.1:7890/callback",
            Browser = new LocalhostBrowser(7890),
            LoadProfile = false  // no userinfo endpoint registered; claims come from the token
        });
    }

    public async Task InitializeAsync()
    {
        var expiryStr = await SecureStorage.Default.GetAsync(ExpiryKey);
        if (expiryStr is not null &&
            DateTimeOffset.TryParse(expiryStr, out var expiry) &&
            expiry > DateTimeOffset.UtcNow)
        {
            _isLoggedIn = true;
        }
    }

    public string? LastError { get; private set; }

    public async Task<bool> LoginAsync()
    {
        LastError = null;
        var result = await _oidcClient.LoginAsync();
        if (result.IsError)
        {
            LastError = result.Error ?? result.ErrorDescription ?? "Unknown error";
            return false;
        }

        await SecureStorage.Default.SetAsync(AccessTokenKey, result.AccessToken ?? string.Empty);
        if (!string.IsNullOrEmpty(result.RefreshToken))
            await SecureStorage.Default.SetAsync(RefreshTokenKey, result.RefreshToken);
        await SecureStorage.Default.SetAsync(ExpiryKey, result.AccessTokenExpiration.ToString("O"));

        _isLoggedIn = true;
        return true;
    }

    public async Task<string?> GetTokenAsync()
    {
        var expiryStr = await SecureStorage.Default.GetAsync(ExpiryKey);
        if (expiryStr is null) return null;
        if (!DateTimeOffset.TryParse(expiryStr, out var expiry)) return null;

        var token = await SecureStorage.Default.GetAsync(AccessTokenKey);

        if (expiry - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(2))
        {
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (refreshToken is not null)
            {
                var result = await _oidcClient.RefreshTokenAsync(refreshToken);
                if (!result.IsError)
                {
                    token = result.AccessToken;
                    await SecureStorage.Default.SetAsync(AccessTokenKey, token ?? string.Empty);
                    if (!string.IsNullOrEmpty(result.RefreshToken))
                        await SecureStorage.Default.SetAsync(RefreshTokenKey, result.RefreshToken);
                    await SecureStorage.Default.SetAsync(ExpiryKey, result.AccessTokenExpiration.ToString("O"));
                }
            }
        }

        return token;
    }

    public async Task LogoutAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiryKey);
        _isLoggedIn = false;
        await Task.CompletedTask;
    }
}
