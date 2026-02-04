using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

namespace Snakk.Web.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public string? ErrorMessage { get; set; }

    public LoginModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public void OnGet(string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            ErrorMessage = error switch
            {
                "oauth_failed" => "OAuth authentication failed. Please try again.",
                "invalid_oauth_response" => "Invalid response from OAuth provider.",
                _ => Uri.UnescapeDataString(error)
            };
        }
    }

    public async Task<IActionResult> OnPostAsync(string email, string password)
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7291";
        var httpClient = _httpClientFactory.CreateClient();

        var loginRequest = new
        {
            email,
            password
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync($"{apiBaseUrl}/auth/login", content);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Extract JWT token from response body
        var responseBody = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

        if (!loginResponse.TryGetProperty("accessToken", out var tokenElement))
        {
            ErrorMessage = "Authentication succeeded but no token was returned.";
            return Page();
        }

        var accessToken = tokenElement.GetString();

        // Redirect to home page with token in URL fragment (auth.js will pick it up)
        return Redirect($"/Index?token={Uri.EscapeDataString(accessToken!)}");
    }
}
