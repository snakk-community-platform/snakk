namespace Snakk.Infrastructure.Services;

using OtpNet;
using Snakk.Application.Services;
using System.Security.Cryptography;
using System.Text;

public class TotpService(IPasswordHasher passwordHasher) : ITotpService
{
    public string GenerateSecret()
    {
        // Generate 20-byte (160-bit) secret for TOTP
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCodeUri(string secret, string accountName, string issuer = "Snakk")
    {
        // Format: otpauth://totp/{issuer}:{accountName}?secret={secret}&issuer={issuer}
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}";
    }

    public bool VerifyCode(string secret, string code, int window = 1) =>
        TryVerifyCode(secret, code, out _, window);

    public bool TryVerifyCode(string secret, string code, out long matchedStep, int window = 1)
    {
        matchedStep = -1;
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);

            // Try current time + window
            var now = DateTime.UtcNow;

            for (var i = -window; i <= window; i++)
            {
                var testTime = now.AddSeconds(i * 30d);
                var expectedCode = totp.ComputeTotp(testTime);

                if (expectedCode == code)
                {
                    // Unix time-step (30s slots since epoch) — uniquely identifies this code.
                    matchedStep = new DateTimeOffset(testTime, TimeSpan.Zero).ToUnixTimeSeconds() / 30;
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public List<string> GenerateBackupCodes(int count = 10)
    {
        var codes = new List<string>();

        for (var i = 0; i < count; i++)
        {
            // Generate 8-character alphanumeric code
            var code = GenerateRandomCode(8);
            codes.Add(code);
        }

        return codes;
    }

    public string HashBackupCode(string code) =>
        passwordHasher.HashPassword(code);

    public bool VerifyBackupCode(string code, string hash) =>
        passwordHasher.VerifyPassword(code, hash);

    private string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluded ambiguous characters (I, O, 0, 1)
        var result = new StringBuilder(length);

        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[length];
        rng.GetBytes(buffer);

        for (var i = 0; i < length; i++)
        {
            result.Append(chars[buffer[i] % chars.Length]);
        }

        return result.ToString();
    }
}
