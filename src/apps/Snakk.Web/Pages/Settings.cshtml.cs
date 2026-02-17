using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

namespace Snakk.Web.Pages;

public class SettingsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public new UserInfo? User { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public SettingsModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadUserAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string displayName, string? currentPassword, string? newPassword, string? confirmPassword)
    {
        await LoadUserAsync();

        if (User == null)
        {
            return RedirectToPage("/Auth/Login");
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

        // Update display name
        if (!string.IsNullOrEmpty(displayName) && displayName != User.DisplayName)
        {
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
                    ErrorMessage = errorObj?.Error ?? "Failed to update display name.";
                }
                catch
                {
                    ErrorMessage = "Failed to update display name.";
                }
                return Page();
            }

            // Reload user to show updated display name
            await LoadUserAsync();
            SuccessMessage = "Display name updated successfully!";
            return Page();
        }

        // Update password if provided
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (string.IsNullOrEmpty(User.OAuthProvider))
            {
                if (newPassword != confirmPassword)
                {
                    ErrorMessage = "New passwords do not match.";
                    return Page();
                }

                if (newPassword.Length < 8)
                {
                    ErrorMessage = "Password must be at least 8 characters.";
                    return Page();
                }

                // TODO: Add API endpoint for password change
                ErrorMessage = "Password change is not yet implemented in the API.";
                return Page();
            }
        }

        SuccessMessage = "Settings saved successfully!";
        return Page();
    }

    private async Task LoadUserAsync()
    {
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

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            User = JsonSerializer.Deserialize<UserInfo>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
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
