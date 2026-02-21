using System.Net.Http.Json;
using System.Text.Json;

namespace Snakk.Sdk;

/// <summary>
/// Manual extensions to the auto-generated SnakkApiClient.
/// This file provides properly-typed wrapper methods for endpoints that need them.
/// </summary>
public partial class SnakkApiClient
{
    /// <summary>
    /// Login with email and password, returning a properly-typed result.
    /// This wraps the generated LoginAsync method with typed response handling.
    /// </summary>
    public async Task<AdminLoginResult?> LoginWithCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            // Make the HTTP call directly since the generated method doesn't return the response
            var response = await _httpClient.PostAsJsonAsync("/auth/login", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<LoginResponseDto>(json, options);

            if (result?.User == null || string.IsNullOrEmpty(result.AccessToken))
                return null;

            return new AdminLoginResult(
                Token: result.AccessToken,
                Username: result.User.DisplayName ?? result.User.Email ?? "Unknown",
                Email: result.User.Email ?? "",
                Roles: new List<string>() // TODO: Get roles from JWT or API response
            );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Result returned from login with typed properties.
    /// </summary>
    public record AdminLoginResult(string Token, string Username, string Email, List<string> Roles);

    #region Private DTOs for response deserialization

    private class LoginResponseDto
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public UserInfoDto? User { get; set; }
    }

    private class UserInfoDto
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public bool EmailVerified { get; set; }
    }

    #endregion
}
