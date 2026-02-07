namespace Snakk.Application.Services;

/// <summary>
/// Service for admin authentication
/// </summary>
public interface IAdminAuthService
{
    /// <summary>
    /// Authenticate an admin user and return a JWT token
    /// </summary>
    /// <param name="email">Admin email</param>
    /// <param name="password">Plain-text password</param>
    /// <returns>JWT token if authentication successful, null otherwise</returns>
    Task<string?> AuthenticateAsync(string email, string password);

    /// <summary>
    /// Verify a JWT token and return the admin user ID
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>Admin user public ID if valid, null otherwise</returns>
    Task<string?> VerifyTokenAsync(string token);
}
