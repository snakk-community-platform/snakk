namespace Snakk.Application.Services;

/// <summary>
/// Encrypts/decrypts email addresses for at-rest protection in the database.
/// Also provides a deterministic hash for email lookups.
/// </summary>
public interface IEmailProtector
{
    string Protect(string email);
    string Unprotect(string protectedEmail);
    string ComputeHash(string email);
}
