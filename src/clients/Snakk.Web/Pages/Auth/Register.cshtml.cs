using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

namespace Snakk.Web.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public RegisterModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string email, string password, string displayName)
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7291";
        var httpClient = _httpClientFactory.CreateClient();

        var registerRequest = new
        {
            email,
            password,
            displayName
        };

        var content = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.PostAsync($"{apiBaseUrl}/auth/register", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            try
            {
                var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                ErrorMessage = errorObj.GetProperty("error").GetString() ?? "Registration failed. Please try again.";
            }
            catch
            {
                ErrorMessage = "Registration failed. Please try again.";
            }
            return Page();
        }

        SuccessMessage = "Account created successfully! Please check your email to verify your account before logging in.";
        return Page();
    }
}
