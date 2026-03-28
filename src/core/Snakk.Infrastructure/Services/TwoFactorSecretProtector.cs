using Microsoft.AspNetCore.DataProtection;
using Snakk.Application.Services;

namespace Snakk.Infrastructure.Services;

/// <summary>
/// Encrypts/decrypts 2FA TOTP secrets using ASP.NET Data Protection API.
/// Keys are managed by the Data Protection system (auto-rotated, machine-specific).
/// </summary>
public class TwoFactorSecretProtector(IDataProtectionProvider dataProtectionProvider) : ITwoFactorSecretProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("Snakk.TwoFactor.Secret.v1");

    public string Protect(string secret) =>
        _protector.Protect(secret);

    public string Unprotect(string protectedSecret) =>
        _protector.Unprotect(protectedSecret);
}
