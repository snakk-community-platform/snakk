using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class UserTests
{
    #region CreateWithEmail Tests

    [Test]
    public async Task CreateWithEmail_WithValidParameters_CreatesUser()
    {
        // Arrange
        const string displayName = "testuser";
        const string email = "test@example.com";
        const string passwordHash = "hashedpassword123";
        const string token = "verification-token";

        // Act
        var user = User.CreateWithEmail(displayName, email, passwordHash, token);

        // Assert
        await Assert.That(user).IsNotNull();
        await Assert.That(user.PublicId).IsNotNull();
        await Assert.That(user.DisplayName).IsEqualTo(displayName);
        await Assert.That(user.Email).IsEqualTo(email);
        await Assert.That(user.PasswordHash).IsEqualTo(passwordHash);
        await Assert.That(user.EmailVerified).IsFalse();
        await Assert.That(user.EmailVerificationToken).IsEqualTo(token);
        await Assert.That(user.OAuthProvider).IsNull();
        await Assert.That(user.OAuthProviderId).IsNull();
        await Assert.That(user.Role).IsNull();
        await Assert.That(user.AvatarFileName).IsNull();
        await Assert.That(user.PreferEndlessScroll).IsTrue();
        await Assert.That(user.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(user.LastSeenAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CreateWithEmail_WithInvalidDisplayName_ThrowsArgumentException(string? invalidDisplayName)
    {
        // Act & Assert
        await Assert.That(() => User.CreateWithEmail(invalidDisplayName!, "test@example.com", "hash", "token")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CreateWithEmail_WithInvalidEmail_ThrowsArgumentException(string? invalidEmail)
    {
        // Act & Assert
        await Assert.That(() => User.CreateWithEmail("displayname", invalidEmail!, "hash", "token")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CreateWithEmail_WithInvalidPasswordHash_ThrowsArgumentException(string? invalidHash)
    {
        // Act & Assert
        await Assert.That(() => User.CreateWithEmail("displayname", "test@example.com", invalidHash!, "token")).Throws<ArgumentException>();
    }

    #endregion

    #region CreateWithOAuth Tests

    [Test]
    public async Task CreateWithOAuth_WithValidParameters_CreatesUser()
    {
        // Arrange
        const string displayName = "oauthuser";
        const string email = "oauth@example.com";
        const string provider = "Google";
        const string providerId = "google-user-id-123";

        // Act
        var user = User.CreateWithOAuth(displayName, email, provider, providerId);

        // Assert
        await Assert.That(user).IsNotNull();
        await Assert.That(user.PublicId).IsNotNull();
        await Assert.That(user.DisplayName).IsEqualTo(displayName);
        await Assert.That(user.Email).IsEqualTo(email);
        await Assert.That(user.PasswordHash).IsNull();
        await Assert.That(user.EmailVerified).IsTrue(); // OAuth emails are pre-verified
        await Assert.That(user.EmailVerificationToken).IsNull();
        await Assert.That(user.OAuthProvider).IsEqualTo(provider);
        await Assert.That(user.OAuthProviderId).IsEqualTo(providerId);
        await Assert.That(user.Role).IsNull();
        await Assert.That(user.PreferEndlessScroll).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CreateWithOAuth_WithInvalidDisplayName_ThrowsArgumentException(string? invalidDisplayName)
    {
        // Act & Assert
        await Assert.That(() => User.CreateWithOAuth(invalidDisplayName!, "oauth@example.com", "Google", "id")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CreateWithOAuth_WithInvalidEmail_ThrowsArgumentException(string? invalidEmail)
    {
        // Act & Assert
        await Assert.That(() => User.CreateWithOAuth("displayname", invalidEmail!, "Google", "id")).Throws<ArgumentException>();
    }

    #endregion

    #region Create Tests (Generic)

    [Test]
    public async Task Create_WithValidDisplayName_CreatesUser()
    {
        // Act
        var user = User.Create("testuser");

        // Assert
        await Assert.That(user).IsNotNull();
        await Assert.That(user.DisplayName).IsEqualTo("testuser");
        await Assert.That(user.Email).IsNull();
        await Assert.That(user.PasswordHash).IsNull();
        await Assert.That(user.EmailVerified).IsFalse();
        await Assert.That(user.OAuthProvider).IsNull();
    }

    [Test]
    public async Task Create_WithEmailParameter_SetsEmail()
    {
        // Act
        var user = User.Create("testuser", email: "test@example.com");

        // Assert
        await Assert.That(user.Email).IsEqualTo("test@example.com");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithInvalidDisplayName_ThrowsArgumentException(string? invalidDisplayName)
    {
        // Act & Assert
        await Assert.That(() => User.Create(invalidDisplayName!)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateDisplayName Tests

    [Test]
    public async Task UpdateDisplayName_WithValidName_UpdatesDisplayName()
    {
        // Arrange
        var user = User.Create("originalname");
        var originalModifiedAt = user.LastModifiedAt;

        // Act
        user.UpdateDisplayName("newname");

        // Assert
        await Assert.That(user.DisplayName).IsEqualTo("newname");
        await Assert.That(user.LastModifiedAt).IsNotEqualTo(originalModifiedAt);
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateDisplayName_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var user = User.Create("originalname");

        // Act & Assert
        await Assert.That(() => user.UpdateDisplayName(invalidName!)).Throws<ArgumentException>();
    }

    #endregion

    #region VerifyEmail Tests

    [Test]
    public async Task VerifyEmail_SetsEmailVerifiedAndClearsToken()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token123");
        await Assert.That(user.EmailVerified).IsFalse();
        await Assert.That(user.EmailVerificationToken).IsEqualTo("token123");

        // Act
        user.VerifyEmail();

        // Assert
        await Assert.That(user.EmailVerified).IsTrue();
        await Assert.That(user.EmailVerificationToken).IsNull();
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Anonymize Tests

    [Test]
    public async Task Anonymize_RemovesPersonalInformation()
    {
        // Arrange
        var user = User.CreateWithEmail("john.doe", "john@example.com", "hash", "token");

        // Act
        user.Anonymize();

        // Assert
        await Assert.That(user.DisplayName).IsEqualTo("Anonymous User");
        await Assert.That(user.Email).IsNull();
        await Assert.That(user.OAuthProviderId).IsNull();
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Anonymize_PreservesUserId()
    {
        // Arrange
        var user = User.Create("testuser");
        var originalId = user.PublicId;

        // Act
        user.Anonymize();

        // Assert
        await Assert.That(user.PublicId).IsEqualTo(originalId);
    }

    #endregion

    #region SetPasswordHash Tests

    [Test]
    public async Task SetPasswordHash_WithValidHash_UpdatesPasswordHash()
    {
        // Arrange
        var user = User.CreateWithOAuth("oauthuser", "oauth@example.com", "Google", "id");
        await Assert.That(user.PasswordHash).IsNull();

        // Act
        user.SetPasswordHash("newhash123");

        // Assert
        await Assert.That(user.PasswordHash).IsEqualTo("newhash123");
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task SetPasswordHash_WithInvalidHash_ThrowsArgumentException(string? invalidHash)
    {
        // Arrange
        var user = User.Create("testuser");

        // Act & Assert
        await Assert.That(() => user.SetPasswordHash(invalidHash!)).Throws<ArgumentException>();
    }

    #endregion

    #region GenerateEmailVerificationToken Tests

    [Test]
    public async Task GenerateEmailVerificationToken_CreatesValidToken()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "oldtoken");

        // Act
        user.GenerateEmailVerificationToken();

        // Assert
        await Assert.That(user.EmailVerificationToken).IsNotNull();
        await Assert.That(user.EmailVerificationToken).IsNotEqualTo("oldtoken");
        await Assert.That(user.EmailVerificationToken!).Length().IsEqualTo(32); // GUID without hyphens
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task GenerateEmailVerificationToken_CreatesUniqueTokensOnMultipleCalls()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        user.GenerateEmailVerificationToken();
        var token1 = user.EmailVerificationToken;

        user.GenerateEmailVerificationToken();
        var token2 = user.EmailVerificationToken;

        // Assert
        await Assert.That(token1).IsNotEqualTo(token2);
    }

    #endregion

    #region HasPassword Tests

    [Test]
    public async Task HasPassword_WithPasswordHash_ReturnsTrue()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        var result = user.HasPassword();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasPassword_WithoutPasswordHash_ReturnsFalse()
    {
        // Arrange
        var user = User.CreateWithOAuth("testuser", "test@example.com", "Google", "id");

        // Act
        var result = user.HasPassword();

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region IsOAuthUser Tests

    [Test]
    public async Task IsOAuthUser_WithOAuthProvider_ReturnsTrue()
    {
        // Arrange
        var user = User.CreateWithOAuth("testuser", "test@example.com", "Google", "id");

        // Act
        var result = user.IsOAuthUser();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsOAuthUser_WithoutOAuthProvider_ReturnsFalse()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        var result = user.IsOAuthUser();

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region Avatar Tests

    [Test]
    public async Task SetAvatarFileName_UpdatesAvatarFileName()
    {
        // Arrange
        var user = User.Create("testuser");
        await Assert.That(user.AvatarFileName).IsNull();

        // Act
        user.SetAvatarFileName("avatar123.jpg");

        // Assert
        await Assert.That(user.AvatarFileName).IsEqualTo("avatar123.jpg");
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ClearAvatar_RemovesAvatarFileName()
    {
        // Arrange
        var user = User.Create("testuser");
        user.SetAvatarFileName("avatar.jpg");
        await Assert.That(user.AvatarFileName).IsNotNull();

        // Act
        user.ClearAvatar();

        // Assert
        await Assert.That(user.AvatarFileName).IsNull();
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    #endregion

    #region LastLogin and LastSeen Tests

    [Test]
    public async Task UpdateLastLogin_UpdatesBothLastLoginAndLastSeen()
    {
        // Arrange
        var user = User.Create("testuser");
        var originalLastLogin = user.LastLoginAt;
        var originalLastSeen = user.LastSeenAt;

        // Act
        user.UpdateLastLogin();

        // Assert
        await Assert.That(user.LastLoginAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(user.LastSeenAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(user.LastLoginAt).IsNotEqualTo(originalLastLogin);
    }

    [Test]
    public async Task UpdateLastSeen_UpdatesOnlyLastSeen()
    {
        // Arrange
        var user = User.Create("testuser");
        user.UpdateLastLogin();
        var lastLoginTime = user.LastLoginAt!.Value;

        // Act
        System.Threading.Thread.Sleep(10); // Small delay to ensure time difference
        user.UpdateLastSeen();

        // Assert
        await Assert.That(user.LastSeenAt!.Value).IsGreaterThan(lastLoginTime);
        await Assert.That(user.LastLoginAt!.Value).IsEqualTo(lastLoginTime); // LastLogin should remain unchanged
    }

    #endregion

    #region PreferEndlessScroll Tests

    [Test]
    public async Task SetPreferEndlessScroll_UpdatesPreference()
    {
        // Arrange
        var user = User.Create("testuser");
        await Assert.That(user.PreferEndlessScroll).IsTrue(); // Default

        // Act
        user.SetPreferEndlessScroll(false);

        // Assert
        await Assert.That(user.PreferEndlessScroll).IsFalse();
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_WithAllParameters_CreatesUserWithExactState()
    {
        // Arrange
        var userId = UserId.New();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var lastModifiedAt = DateTime.UtcNow.AddDays(-5);
        var lastSeenAt = DateTime.UtcNow.AddHours(-2);
        var lastLoginAt = DateTime.UtcNow.AddHours(-3);

        // Act
        var user = User.Rehydrate(
            userId,
            "testuser",
            "test@example.com",
            "passwordhash",
            true,
            null,
            "Google",
            "google-id",
            "admin",
            "avatar.jpg",
            0,
            false,
            false,
            createdAt,
            lastModifiedAt,
            lastSeenAt,
            lastLoginAt);

        // Assert
        await Assert.That(user.PublicId).IsEqualTo(userId);
        await Assert.That(user.DisplayName).IsEqualTo("testuser");
        await Assert.That(user.Email).IsEqualTo("test@example.com");
        await Assert.That(user.PasswordHash).IsEqualTo("passwordhash");
        await Assert.That(user.EmailVerified).IsTrue();
        await Assert.That(user.EmailVerificationToken).IsNull();
        await Assert.That(user.OAuthProvider).IsEqualTo("Google");
        await Assert.That(user.OAuthProviderId).IsEqualTo("google-id");
        await Assert.That(user.Role).IsEqualTo("admin");
        await Assert.That(user.AvatarFileName).IsEqualTo("avatar.jpg");
        await Assert.That(user.PreferEndlessScroll).IsFalse();
        await Assert.That(user.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(user.LastModifiedAt).IsEqualTo(lastModifiedAt);
        await Assert.That(user.LastSeenAt).IsEqualTo(lastSeenAt);
        await Assert.That(user.LastLoginAt).IsEqualTo(lastLoginAt);
    }

    #endregion
}
