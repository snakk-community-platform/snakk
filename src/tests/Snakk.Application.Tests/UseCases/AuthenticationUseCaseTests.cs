using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class AuthenticationUseCaseTests
{
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IPasswordHasher> _mockPasswordHasher = new();
    private readonly Mock<IEmailSender> _mockEmailSender = new();
    private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepository = new();
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher = new();
    private AuthenticationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new AuthenticationUseCase(
            _mockUserRepository.Object,
            _mockPasswordHasher.Object,
            _mockEmailSender.Object,
            _mockRefreshTokenRepository.Object,
            _mockEventDispatcher.Object);
    }

    #region RegisterWithEmailAsync Tests

    [Test]
    public async Task RegisterWithEmailAsync_WithValidParameters_CreatesUser()
    {
        // Arrange
        const string email = "test@example.com";
        const string password = "Password123!";
        const string displayName = "TestUser";
        const string baseUrl = "https://example.com";
        const string passwordHash = "hashed_password";

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User>());
        _mockPasswordHasher.Setup(h => h.HashPassword(password))
            .Returns(passwordHash);

        // Act
        var result = await _useCase.RegisterWithEmailAsync(email, password, displayName, baseUrl);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Email).IsEqualTo(email);
        await Assert.That(result.Value.DisplayName).IsEqualTo(displayName);
        await Assert.That(result.Value.EmailVerified).IsFalse();

        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _mockEmailSender.Verify(
            e => e.SendEmailVerificationAsync(email, displayName, It.IsAny<string>(), baseUrl),
            Times.Once);
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithEmptyEmail_ReturnsFailure()
    {
        // Act
        var result = await _useCase.RegisterWithEmailAsync("", "Password123!", "TestUser", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithNullEmail_ReturnsFailure()
    {
        // Act
        var result = await _useCase.RegisterWithEmailAsync(null!, "Password123!", "TestUser", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithWhitespaceEmail_ReturnsFailure()
    {
        // Act
        var result = await _useCase.RegisterWithEmailAsync("   ", "Password123!", "TestUser", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithEmptyPassword_ReturnsFailure()
    {
        // Act
        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "", "TestUser", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Password is required");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithPasswordLessThan8Characters_ReturnsFailure()
    {
        // Act
        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "Pass123", "TestUser", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Password must be at least 8 characters");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithExactly8CharacterPassword_Succeeds()
    {
        // Arrange
        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User>());
        _mockPasswordHasher.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed");

        // Act
        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "Pass1234", "TestUser", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithEmptyDisplayName_ReturnsFailure()
    {
        // Act
        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "Password123!", "", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Display name is required");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithExistingEmail_ReturnsFailure()
    {
        // Arrange
        const string email = "existing@example.com";
        var existingUser = User.CreateWithEmail("ExistingUser", email, "hash", "token");

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _useCase.RegisterWithEmailAsync(email, "Password123!", "NewUser", "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is already registered");
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithTakenDisplayName_GeneratesUniqueDisplayName()
    {
        // Arrange
        const string displayName = "TestUser";
        const string email = "new@example.com";
        var existingUser = User.CreateWithEmail(displayName, "existing@example.com", "hash", "token");

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { existingUser });
        _mockPasswordHasher.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed");

        // Act
        var result = await _useCase.RegisterWithEmailAsync(email, "Password123!", displayName, "https://example.com");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.DisplayName).IsNotEqualTo(displayName);
        await Assert.That(result.Value.DisplayName).StartsWith(displayName);
    }

    #endregion

    #region LoginWithEmailAsync Tests

    [Test]
    public async Task LoginWithEmailAsync_WithValidCredentials_ReturnsUser()
    {
        // Arrange
        const string email = "test@example.com";
        const string password = "Password123!";
        const string passwordHash = "hashed_password";

        var user = User.CreateWithEmail("TestUser", email, passwordHash, "token");

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(user);
        _mockPasswordHasher.Setup(h => h.VerifyPassword(password, passwordHash))
            .Returns(true);

        // Act
        var result = await _useCase.LoginWithEmailAsync(email, password);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Email).IsEqualTo(email);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Once);
    }

    [Test]
    public async Task LoginWithEmailAsync_WithEmptyEmail_ReturnsFailure()
    {
        // Act
        var result = await _useCase.LoginWithEmailAsync("", "Password123!");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithEmptyPassword_ReturnsFailure()
    {
        // Act
        var result = await _useCase.LoginWithEmailAsync("test@example.com", "");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Password is required");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithNonExistentEmail_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.LoginWithEmailAsync("nonexistent@example.com", "Password123!");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid email or password");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithInvalidPassword_ReturnsFailure()
    {
        // Arrange
        const string email = "test@example.com";
        var user = User.CreateWithEmail("TestUser", email, "correct_hash", "token");

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(user);
        _mockPasswordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _useCase.LoginWithEmailAsync(email, "WrongPassword");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid email or password");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithOAuthUser_ReturnsFailure()
    {
        // Arrange - OAuth user has no password
        const string email = "oauth@example.com";
        var oauthUser = User.CreateWithOAuth("OAuthUser", email, "google", "google123");

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(oauthUser);

        // Act
        var result = await _useCase.LoginWithEmailAsync(email, "AnyPassword");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid email or password");
    }

    #endregion

    #region LoginWithOAuthAsync Tests

    [Test]
    public async Task LoginWithOAuthAsync_WithExistingOAuthUser_ReturnsUser()
    {
        // Arrange
        const string oauthProviderId = "google123";
        var existingUser = User.CreateWithOAuth("ExistingUser", "test@example.com", "google", oauthProviderId);

        _mockUserRepository.Setup(r => r.GetByOAuthProviderIdAsync(oauthProviderId))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _useCase.LoginWithOAuthAsync("google", oauthProviderId, "test@example.com", "TestUser");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(existingUser);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Once);
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task LoginWithOAuthAsync_WithNewOAuthUser_CreatesUser()
    {
        // Arrange
        const string email = "newuser@example.com";
        const string displayName = "NewUser";
        const string oauthProvider = "google";
        const string oauthProviderId = "google456";

        _mockUserRepository.Setup(r => r.GetByOAuthProviderIdAsync(oauthProviderId))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _useCase.LoginWithOAuthAsync(oauthProvider, oauthProviderId, email, displayName);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Email).IsEqualTo(email);
        await Assert.That(result.Value.EmailVerified).IsTrue();
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _mockEmailSender.Verify(e => e.SendWelcomeEmailAsync(email, It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task LoginWithOAuthAsync_WithExistingEmail_ReturnsFailure()
    {
        // Arrange - Email exists but not linked to OAuth
        const string email = "existing@example.com";
        var existingEmailUser = User.CreateWithEmail("ExistingUser", email, "hash", "token");

        _mockUserRepository.Setup(r => r.GetByOAuthProviderIdAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(existingEmailUser);

        // Act
        var result = await _useCase.LoginWithOAuthAsync("google", "google789", email, "DisplayName");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("already exists");
        await Assert.That(result.Error).Contains("login with your password");
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    #endregion

    #region VerifyEmailAsync Tests

    [Test]
    public async Task VerifyEmailAsync_WithValidToken_VerifiesEmail()
    {
        // Arrange
        const string token = "valid_token_123";
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", token);

        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { user });

        // Act
        var result = await _useCase.VerifyEmailAsync(token);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.EmailVerified).IsTrue();
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Test]
    public async Task VerifyEmailAsync_WithEmptyToken_ReturnsFailure()
    {
        // Act
        var result = await _useCase.VerifyEmailAsync("");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Verification token is required");
    }

    [Test]
    public async Task VerifyEmailAsync_WithInvalidToken_ReturnsFailure()
    {
        // Arrange
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _useCase.VerifyEmailAsync("invalid_token");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid or expired verification token");
    }

    [Test]
    public async Task VerifyEmailAsync_WhenAlreadyVerified_ReturnsFailure()
    {
        // Arrange
        const string token = "token_123";
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", token);
        user.VerifyEmail(); // Already verified - this sets the token to null

        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { user });

        // Act
        var result = await _useCase.VerifyEmailAsync(token);

        // Assert
        // Note: After verification, the token is set to null, so when we try to verify again
        // with the same token, the user lookup by token returns null (no user has this token)
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid or expired verification token");
    }

    #endregion

    #region UpdateDisplayNameAsync Tests

    [Test]
    public async Task UpdateDisplayNameAsync_WithAvailableName_UpdatesDisplayName()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateWithEmail("OldName", "test@example.com", "hash", "token");
        const string newDisplayName = "NewName";

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { user });

        // Act
        var result = await _useCase.UpdateDisplayNameAsync(userId, newDisplayName);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.DisplayName).IsEqualTo(newDisplayName);
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Test]
    public async Task UpdateDisplayNameAsync_WithTakenName_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateWithEmail("CurrentName", "test@example.com", "hash", "token");
        var otherUser = User.CreateWithEmail("TakenName", "other@example.com", "hash", "token");

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { user, otherUser });

        // Act
        var result = await _useCase.UpdateDisplayNameAsync(userId, "TakenName");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("is taken");
        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Test]
    public async Task UpdateDisplayNameAsync_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var result = await _useCase.UpdateDisplayNameAsync(userId, "");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Display name cannot be empty");
    }

    [Test]
    public async Task UpdateDisplayNameAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.UpdateDisplayNameAsync(userId, "NewName");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion

    #region UpdatePreferencesAsync Tests

    [Test]
    public async Task UpdatePreferencesAsync_WithValidPreference_UpdatesUser()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.UpdatePreferencesAsync(userId, preferEndlessScroll: true, autoFollowOnReply: null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.PreferEndlessScroll).IsTrue();
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Test]
    public async Task UpdatePreferencesAsync_WithNullPreference_DoesNotUpdateAnything()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var originalPreference = user.PreferEndlessScroll;

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.UpdatePreferencesAsync(userId, preferEndlessScroll: null, autoFollowOnReply: null);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.PreferEndlessScroll).IsEqualTo(originalPreference);
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once); // Still updates (could be optimized)
    }

    [Test]
    public async Task UpdatePreferencesAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.UpdatePreferencesAsync(userId, preferEndlessScroll: true, autoFollowOnReply: null);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion

    #region GetUserByIdAsync Tests

    [Test]
    public async Task GetUserByIdAsync_WithExistingUser_ReturnsUser()
    {
        // Arrange
        var userId = UserId.New();
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.GetUserByIdAsync(userId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(user);
    }

    [Test]
    public async Task GetUserByIdAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.GetUserByIdAsync(userId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion
}
