using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Xunit;

namespace Snakk.Domain.Tests.Entities;

public class UserTests
{
    #region CreateWithEmail Tests

    [Fact]
    public void CreateWithEmail_WithValidParameters_CreatesUser()
    {
        // Arrange
        const string displayName = "testuser";
        const string email = "test@example.com";
        const string passwordHash = "hashedpassword123";
        const string token = "verification-token";

        // Act
        var user = User.CreateWithEmail(displayName, email, passwordHash, token);

        // Assert
        user.Should().NotBeNull();
        user.PublicId.Should().NotBeNull();
        user.DisplayName.Should().Be(displayName);
        user.Email.Should().Be(email);
        user.PasswordHash.Should().Be(passwordHash);
        user.EmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().Be(token);
        user.OAuthProvider.Should().BeNull();
        user.OAuthProviderId.Should().BeNull();
        user.Role.Should().BeNull();
        user.AvatarFileName.Should().BeNull();
        user.PreferEndlessScroll.Should().BeTrue();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithEmail_WithInvalidDisplayName_ThrowsArgumentException(string? invalidDisplayName)
    {
        // Act
        Action act = () => User.CreateWithEmail(invalidDisplayName!, "test@example.com", "hash", "token");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display name cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithEmail_WithInvalidEmail_ThrowsArgumentException(string? invalidEmail)
    {
        // Act
        Action act = () => User.CreateWithEmail("displayname", invalidEmail!, "hash", "token");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithEmail_WithInvalidPasswordHash_ThrowsArgumentException(string? invalidHash)
    {
        // Act
        Action act = () => User.CreateWithEmail("displayname", "test@example.com", invalidHash!, "token");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Password hash cannot be empty*");
    }

    #endregion

    #region CreateWithOAuth Tests

    [Fact]
    public void CreateWithOAuth_WithValidParameters_CreatesUser()
    {
        // Arrange
        const string displayName = "oauthuser";
        const string email = "oauth@example.com";
        const string provider = "Google";
        const string providerId = "google-user-id-123";

        // Act
        var user = User.CreateWithOAuth(displayName, email, provider, providerId);

        // Assert
        user.Should().NotBeNull();
        user.PublicId.Should().NotBeNull();
        user.DisplayName.Should().Be(displayName);
        user.Email.Should().Be(email);
        user.PasswordHash.Should().BeNull();
        user.EmailVerified.Should().BeTrue(); // OAuth emails are pre-verified
        user.EmailVerificationToken.Should().BeNull();
        user.OAuthProvider.Should().Be(provider);
        user.OAuthProviderId.Should().Be(providerId);
        user.Role.Should().BeNull();
        user.PreferEndlessScroll.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithOAuth_WithInvalidDisplayName_ThrowsArgumentException(string? invalidDisplayName)
    {
        // Act
        Action act = () => User.CreateWithOAuth(invalidDisplayName!, "oauth@example.com", "Google", "id");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display name cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithOAuth_WithInvalidEmail_ThrowsArgumentException(string? invalidEmail)
    {
        // Act
        Action act = () => User.CreateWithOAuth("displayname", invalidEmail!, "Google", "id");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email cannot be empty*");
    }

    #endregion

    #region Create Tests (Generic)

    [Fact]
    public void Create_WithValidDisplayName_CreatesUser()
    {
        // Act
        var user = User.Create("testuser");

        // Assert
        user.Should().NotBeNull();
        user.DisplayName.Should().Be("testuser");
        user.Email.Should().BeNull();
        user.PasswordHash.Should().BeNull();
        user.EmailVerified.Should().BeFalse();
        user.OAuthProvider.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmailParameter_SetsEmail()
    {
        // Act
        var user = User.Create("testuser", email: "test@example.com");

        // Assert
        user.Email.Should().Be("test@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDisplayName_ThrowsArgumentException(string? invalidDisplayName)
    {
        // Act
        Action act = () => User.Create(invalidDisplayName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display name cannot be empty*");
    }

    #endregion

    #region UpdateDisplayName Tests

    [Fact]
    public void UpdateDisplayName_WithValidName_UpdatesDisplayName()
    {
        // Arrange
        var user = User.Create("originalname");
        var originalModifiedAt = user.LastModifiedAt;

        // Act
        user.UpdateDisplayName("newname");

        // Assert
        user.DisplayName.Should().Be("newname");
        user.LastModifiedAt.Should().NotBe(originalModifiedAt);
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var user = User.Create("originalname");

        // Act
        Action act = () => user.UpdateDisplayName(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display name cannot be empty*");
    }

    #endregion

    #region VerifyEmail Tests

    [Fact]
    public void VerifyEmail_SetsEmailVerifiedAndClearsToken()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token123");
        user.EmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().Be("token123");

        // Act
        user.VerifyEmail();

        // Assert
        user.EmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Anonymize Tests

    [Fact]
    public void Anonymize_RemovesPersonalInformation()
    {
        // Arrange
        var user = User.CreateWithEmail("john.doe", "john@example.com", "hash", "token");

        // Act
        user.Anonymize();

        // Assert
        user.DisplayName.Should().Be("Anonymous User");
        user.Email.Should().BeNull();
        user.OAuthProviderId.Should().BeNull();
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Anonymize_PreservesUserId()
    {
        // Arrange
        var user = User.Create("testuser");
        var originalId = user.PublicId;

        // Act
        user.Anonymize();

        // Assert
        user.PublicId.Should().Be(originalId);
    }

    #endregion

    #region SetPasswordHash Tests

    [Fact]
    public void SetPasswordHash_WithValidHash_UpdatesPasswordHash()
    {
        // Arrange
        var user = User.CreateWithOAuth("oauthuser", "oauth@example.com", "Google", "id");
        user.PasswordHash.Should().BeNull();

        // Act
        user.SetPasswordHash("newhash123");

        // Assert
        user.PasswordHash.Should().Be("newhash123");
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_WithInvalidHash_ThrowsArgumentException(string? invalidHash)
    {
        // Arrange
        var user = User.Create("testuser");

        // Act
        Action act = () => user.SetPasswordHash(invalidHash!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Password hash cannot be empty*");
    }

    #endregion

    #region GenerateEmailVerificationToken Tests

    [Fact]
    public void GenerateEmailVerificationToken_CreatesValidToken()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "oldtoken");

        // Act
        user.GenerateEmailVerificationToken();

        // Assert
        user.EmailVerificationToken.Should().NotBeNullOrEmpty();
        user.EmailVerificationToken.Should().NotBe("oldtoken");
        user.EmailVerificationToken.Should().HaveLength(32); // GUID without hyphens
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GenerateEmailVerificationToken_CreatesUniqueTokensOnMultipleCalls()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        user.GenerateEmailVerificationToken();
        var token1 = user.EmailVerificationToken;

        user.GenerateEmailVerificationToken();
        var token2 = user.EmailVerificationToken;

        // Assert
        token1.Should().NotBe(token2);
    }

    #endregion

    #region HasPassword Tests

    [Fact]
    public void HasPassword_WithPasswordHash_ReturnsTrue()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        var result = user.HasPassword();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPassword_WithoutPasswordHash_ReturnsFalse()
    {
        // Arrange
        var user = User.CreateWithOAuth("testuser", "test@example.com", "Google", "id");

        // Act
        var result = user.HasPassword();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsOAuthUser Tests

    [Fact]
    public void IsOAuthUser_WithOAuthProvider_ReturnsTrue()
    {
        // Arrange
        var user = User.CreateWithOAuth("testuser", "test@example.com", "Google", "id");

        // Act
        var result = user.IsOAuthUser();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOAuthUser_WithoutOAuthProvider_ReturnsFalse()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        var result = user.IsOAuthUser();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Avatar Tests

    [Fact]
    public void SetAvatarFileName_UpdatesAvatarFileName()
    {
        // Arrange
        var user = User.Create("testuser");
        user.AvatarFileName.Should().BeNull();

        // Act
        user.SetAvatarFileName("avatar123.jpg");

        // Assert
        user.AvatarFileName.Should().Be("avatar123.jpg");
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ClearAvatar_RemovesAvatarFileName()
    {
        // Arrange
        var user = User.Create("testuser");
        user.SetAvatarFileName("avatar.jpg");
        user.AvatarFileName.Should().NotBeNull();

        // Act
        user.ClearAvatar();

        // Assert
        user.AvatarFileName.Should().BeNull();
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region LastLogin and LastSeen Tests

    [Fact]
    public void UpdateLastLogin_UpdatesBothLastLoginAndLastSeen()
    {
        // Arrange
        var user = User.Create("testuser");
        var originalLastLogin = user.LastLoginAt;
        var originalLastSeen = user.LastSeenAt;

        // Act
        user.UpdateLastLogin();

        // Assert
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.LastLoginAt.Should().NotBe(originalLastLogin);
    }

    [Fact]
    public void UpdateLastSeen_UpdatesOnlyLastSeen()
    {
        // Arrange
        var user = User.Create("testuser");
        user.UpdateLastLogin();
        var lastLoginTime = user.LastLoginAt;

        // Act
        System.Threading.Thread.Sleep(10); // Small delay to ensure time difference
        user.UpdateLastSeen();

        // Assert
        user.LastSeenAt.Should().BeAfter(lastLoginTime!.Value);
        user.LastLoginAt.Should().Be(lastLoginTime); // LastLogin should remain unchanged
    }

    #endregion

    #region PreferEndlessScroll Tests

    [Fact]
    public void SetPreferEndlessScroll_UpdatesPreference()
    {
        // Arrange
        var user = User.Create("testuser");
        user.PreferEndlessScroll.Should().BeTrue(); // Default

        // Act
        user.SetPreferEndlessScroll(false);

        // Assert
        user.PreferEndlessScroll.Should().BeFalse();
        user.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Rehydrate Tests

    [Fact]
    public void Rehydrate_WithAllParameters_CreatesUserWithExactState()
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
            false,
            createdAt,
            lastModifiedAt,
            lastSeenAt,
            lastLoginAt);

        // Assert
        user.PublicId.Should().Be(userId);
        user.DisplayName.Should().Be("testuser");
        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("passwordhash");
        user.EmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.OAuthProvider.Should().Be("Google");
        user.OAuthProviderId.Should().Be("google-id");
        user.Role.Should().Be("admin");
        user.AvatarFileName.Should().Be("avatar.jpg");
        user.PreferEndlessScroll.Should().BeFalse();
        user.CreatedAt.Should().Be(createdAt);
        user.LastModifiedAt.Should().Be(lastModifiedAt);
        user.LastSeenAt.Should().Be(lastSeenAt);
        user.LastLoginAt.Should().Be(lastLoginAt);
    }

    #endregion
}
