namespace Snakk.Application.Services;

/// <summary>
/// Application-layer abstraction for password hashing.
/// Implementations should use secure algorithms like BCrypt.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hash a plain-text password
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verify a plain-text password against a hash
    /// </summary>
    bool VerifyPassword(string password, string hash);
}
