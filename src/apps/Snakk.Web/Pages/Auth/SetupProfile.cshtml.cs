using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

namespace Snakk.Web.Pages.Auth;

public class SetupProfileModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public string? SuggestedDisplayName { get; set; }
    public string? ErrorMessage { get; set; }
    public string ApiBaseUrl => _configuration["ApiBaseUrl"] ?? "https://localhost:7291";

    public SetupProfileModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Get current user info
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7291";
        var httpClient = _httpClientFactory.CreateClient();

        // Copy cookies from request to API call
        var cookies = Request.Cookies;
        if (cookies.Any())
        {
            var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
            httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }

        var response = await httpClient.GetAsync($"{apiBaseUrl}/auth/me");

        if (!response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Auth/Login");
        }

        var content = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<UserInfo>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Don't pre-fill display name to avoid showing user's real name from OAuth
        // User should choose their own display name

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ErrorMessage = "Display name is required.";
            return Page();
        }

        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7291";
        var httpClient = _httpClientFactory.CreateClient();

        // Copy cookies from request to API call
        var cookies = Request.Cookies;
        if (cookies.Any())
        {
            var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
            httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }

        // Call API to update display name
        var updateRequest = new { displayName };
        var requestContent = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json");

        var updateResponse = await httpClient.PutAsync($"{apiBaseUrl}/auth/update-profile", requestContent);

        if (!updateResponse.IsSuccessStatusCode)
        {
            var errorContent = await updateResponse.Content.ReadAsStringAsync();
            try
            {
                var errorObj = JsonSerializer.Deserialize<ErrorResponse>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                ErrorMessage = errorObj?.Error ?? $"Failed to update display name. Status: {updateResponse.StatusCode}";
            }
            catch
            {
                ErrorMessage = $"Failed to update display name. Status: {updateResponse.StatusCode}";
            }
            SuggestedDisplayName = displayName;
            return Page();
        }

        // Forward Set-Cookie headers from API response to browser
        if (updateResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var cookie in setCookieHeaders)
            {
                Response.Headers.Append("Set-Cookie", cookie);
            }
        }

        // Redirect to home page
        return RedirectToPage("/Index");
    }

    public class UserInfo
    {
        public string PublicId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Email { get; set; }
        public bool EmailVerified { get; set; }
        public string? OAuthProvider { get; set; }
    }

    public class ErrorResponse
    {
        public string? Error { get; set; }
    }
}
