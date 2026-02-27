using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    #region HashPassword Tests

    [Test]
    public async Task HashPassword_WithValidPassword_ReturnsHash()
    {
        // Act
        var hash = _hasher.HashPassword("MySecurePassword123!");

        // Assert
        await Assert.That(hash).IsNotNull();
        await Assert.That(hash.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task HashPassword_WithSamePassword_ReturnsDifferentHashes()
    {
        // BCrypt uses random salts, so same password should produce different hashes
        var hash1 = _hasher.HashPassword("password123");
        var hash2 = _hasher.HashPassword("password123");

        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [Test]
    public async Task HashPassword_WithNullPassword_ThrowsArgumentException()
    {
        await Assert.That(() => _hasher.HashPassword(null!)).ThrowsException();
    }

    [Test]
    public async Task HashPassword_WithEmptyPassword_ThrowsArgumentException()
    {
        await Assert.That(() => _hasher.HashPassword("")).ThrowsException();
    }

    [Test]
    public async Task HashPassword_ReturnsBCryptFormatHash()
    {
        var hash = _hasher.HashPassword("test");

        // BCrypt hashes start with $2a$, $2b$, or $2y$ followed by cost factor
        await Assert.That(hash.StartsWith("$2")).IsTrue();
    }

    #endregion

    #region VerifyPassword Tests

    [Test]
    public async Task VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var password = "CorrectPassword!";
        var hash = _hasher.HashPassword(password);

        var result = _hasher.VerifyPassword(password, hash);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("CorrectPassword!");

        var result = _hasher.VerifyPassword("WrongPassword!", hash);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyPassword_WithNullPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("password");

        var result = _hasher.VerifyPassword(null!, hash);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("password");

        var result = _hasher.VerifyPassword("", hash);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyPassword_WithNullHash_ReturnsFalse()
    {
        var result = _hasher.VerifyPassword("password", null!);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyPassword_WithEmptyHash_ReturnsFalse()
    {
        var result = _hasher.VerifyPassword("password", "");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyPassword_WithInvalidHash_ReturnsFalse()
    {
        var result = _hasher.VerifyPassword("password", "not-a-valid-bcrypt-hash");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyPassword_WithSpecialCharacters_WorksCorrectly()
    {
        var password = "P@$$w0rd!#%^&*()_+{}|:<>?";
        var hash = _hasher.HashPassword(password);

        var result = _hasher.VerifyPassword(password, hash);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyPassword_WithLongPassword_WorksCorrectly()
    {
        var password = new string('A', 72); // BCrypt max is typically 72 bytes
        var hash = _hasher.HashPassword(password);

        var result = _hasher.VerifyPassword(password, hash);

        await Assert.That(result).IsTrue();
    }

    #endregion
}
