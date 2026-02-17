using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Snakk.Web.Pages;

public class ProfileModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public new UserInfo? User { get; set; }

    public ProfileModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IActionResult> OnGetAsync()
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

        if (!response.IsSuccessStatusCode)
        {
            return Page();
        }

        var content = await response.Content.ReadAsStringAsync();
        User = JsonSerializer.Deserialize<UserInfo>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
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

        await httpClient.PostAsync($"{apiBaseUrl}/auth/logout", null);

        // Clear cookies
        foreach (var cookie in Request.Cookies.Keys)
        {
            Response.Cookies.Delete(cookie);
        }

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
}
