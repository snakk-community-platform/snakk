using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Snakk.Application.Services;

namespace Snakk.Infrastructure.Services;

public class EmailProtector(IDataProtectionProvider dataProtectionProvider) : IEmailProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("Snakk.Email.v1");

    public string Protect(string email) => _protector.Protect(email);
    public string Unprotect(string protectedEmail) => _protector.Unprotect(protectedEmail);

    public string ComputeHash(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexStringLower(bytes);
    }
}
