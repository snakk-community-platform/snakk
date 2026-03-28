namespace Snakk.Application.Services;

/// <summary>
/// Encrypts and decrypts 2FA TOTP secrets for at-rest protection in the database.
/// </summary>
public interface ITwoFactorSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}
