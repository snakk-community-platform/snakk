using Moq;
using Snakk.Application.Services;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class TotpServiceTests
{
    private readonly TotpService _totpService;
    private readonly BCryptPasswordHasher _passwordHasher;

    public TotpServiceTests()
    {
        _passwordHasher = new BCryptPasswordHasher();
        _totpService = new TotpService(_passwordHasher);
    }

    #region GenerateSecret Tests

    [Test]
    public async Task GenerateSecret_ReturnsNonEmptyString()
    {
        var secret = _totpService.GenerateSecret();

        await Assert.That(secret).IsNotNull();
        await Assert.That(secret.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GenerateSecret_ReturnsDifferentSecrets()
    {
        var secret1 = _totpService.GenerateSecret();
        var secret2 = _totpService.GenerateSecret();

        await Assert.That(secret1).IsNotEqualTo(secret2);
    }

    [Test]
    public async Task GenerateSecret_ReturnsBase32EncodedString()
    {
        var secret = _totpService.GenerateSecret();

        // Base32 characters: A-Z, 2-7
        var isBase32 = secret.All(c => (c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7') || c == '=');
        await Assert.That(isBase32).IsTrue();
    }

    #endregion

    #region GenerateQrCodeUri Tests

    [Test]
    public async Task GenerateQrCodeUri_ReturnsOtpauthUri()
    {
        var secret = _totpService.GenerateSecret();
        var uri = _totpService.GenerateQrCodeUri(secret, "user@example.com", "Snakk");

        await Assert.That(uri.StartsWith("otpauth://totp/")).IsTrue();
    }

    [Test]
    public async Task GenerateQrCodeUri_ContainsSecret()
    {
        var secret = _totpService.GenerateSecret();
        var uri = _totpService.GenerateQrCodeUri(secret, "user@example.com");

        await Assert.That(uri).Contains($"secret={secret}");
    }

    [Test]
    public async Task GenerateQrCodeUri_ContainsIssuer()
    {
        var secret = _totpService.GenerateSecret();
        var uri = _totpService.GenerateQrCodeUri(secret, "user@example.com", "Snakk");

        await Assert.That(uri).Contains("issuer=Snakk");
    }

    [Test]
    public async Task GenerateQrCodeUri_EncodesAccountName()
    {
        var secret = _totpService.GenerateSecret();
        var uri = _totpService.GenerateQrCodeUri(secret, "user with spaces@example.com");

        // Should be URL-encoded
        await Assert.That(uri).Contains(Uri.EscapeDataString("user with spaces@example.com"));
    }

    [Test]
    public async Task GenerateQrCodeUri_DefaultIssuerIsSnakk()
    {
        var secret = _totpService.GenerateSecret();
        var uri = _totpService.GenerateQrCodeUri(secret, "user@example.com");

        await Assert.That(uri).Contains("Snakk");
    }

    #endregion

    #region VerifyCode Tests

    [Test]
    public async Task VerifyCode_WithValidCode_ReturnsTrue()
    {
        var secret = _totpService.GenerateSecret();

        // Generate a valid code using OtpNet directly
        var secretBytes = OtpNet.Base32Encoding.ToBytes(secret);
        var totp = new OtpNet.Totp(secretBytes);
        var code = totp.ComputeTotp(DateTime.UtcNow);

        var result = _totpService.VerifyCode(secret, code);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyCode_WithInvalidCode_ReturnsFalse()
    {
        var secret = _totpService.GenerateSecret();

        var result = _totpService.VerifyCode(secret, "000000");

        // Might be true if it happens to match, but very unlikely
        // This is a probabilistic test; a fixed invalid code is more reliable
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyCode_WithInvalidSecret_ReturnsFalse()
    {
        var result = _totpService.VerifyCode("INVALIDBASE32!!!", "123456");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyCode_WithEmptyCode_ReturnsFalse()
    {
        var secret = _totpService.GenerateSecret();

        var result = _totpService.VerifyCode(secret, "");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GenerateBackupCodes Tests

    [Test]
    public async Task GenerateBackupCodes_ReturnsCorrectCount()
    {
        var codes = _totpService.GenerateBackupCodes(10);

        await Assert.That(codes.Count).IsEqualTo(10);
    }

    [Test]
    public async Task GenerateBackupCodes_ReturnsCustomCount()
    {
        var codes = _totpService.GenerateBackupCodes(5);

        await Assert.That(codes.Count).IsEqualTo(5);
    }

    [Test]
    public async Task GenerateBackupCodes_ReturnsUniqueCodes()
    {
        var codes = _totpService.GenerateBackupCodes(10);
        var uniqueCodes = codes
            .Distinct()
            .ToList();

        await Assert.That(uniqueCodes.Count).IsEqualTo(codes.Count);
    }

    [Test]
    public async Task GenerateBackupCodes_ReturnsEightCharacterCodes()
    {
        var codes = _totpService.GenerateBackupCodes(10);

        foreach (var code in codes)
        {
            await Assert.That(code.Length).IsEqualTo(8);
        }
    }

    [Test]
    public async Task GenerateBackupCodes_ExcludesAmbiguousCharacters()
    {
        // Generate many codes to test character set
        var codes = _totpService.GenerateBackupCodes(100);

        foreach (var code in codes)
        {
            await Assert.That(code.Contains('I')).IsFalse();
            await Assert.That(code.Contains('O')).IsFalse();
            await Assert.That(code.Contains('0')).IsFalse();
            await Assert.That(code.Contains('1')).IsFalse();
        }
    }

    #endregion

    #region HashBackupCode / VerifyBackupCode Tests

    [Test]
    public async Task HashBackupCode_ReturnsNonEmptyHash()
    {
        var hash = _totpService.HashBackupCode("ABCD1234");

        await Assert.That(hash).IsNotNull();
        await Assert.That(hash.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task VerifyBackupCode_WithCorrectCode_ReturnsTrue()
    {
        var code = "TESTCODE";
        var hash = _totpService.HashBackupCode(code);

        var result = _totpService.VerifyBackupCode(code, hash);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyBackupCode_WithIncorrectCode_ReturnsFalse()
    {
        var hash = _totpService.HashBackupCode("CORRECT1");

        var result = _totpService.VerifyBackupCode("WRONGCODE", hash);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task RoundTrip_GenerateAndVerifyBackupCode_Works()
    {
        var codes = _totpService.GenerateBackupCodes(5);
        var firstCode = codes.First();
        var hash = _totpService.HashBackupCode(firstCode);

        // Should verify the same code
        var result = _totpService.VerifyBackupCode(firstCode, hash);
        await Assert.That(result).IsTrue();

        // Should not verify a different code
        var result2 = _totpService.VerifyBackupCode(codes.Last(), hash);
        await Assert.That(result2).IsFalse();
    }

    #endregion
}
