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

        // Copy authentication cookie from API response to web client response
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                Response.Headers.Append("Set-Cookie", cookie);
            }
        }

        return RedirectToPage("/Index");
    }
}
