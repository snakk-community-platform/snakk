using Moq;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;

namespace Snakk.Application.Tests.Scenarios;

/// <summary>
/// Comprehensive workflow tests for user registration scenarios
/// </summary>
public class UserRegistrationWorkflowTests
{
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IPasswordHasher> _mockPasswordHasher = new();
    private readonly Mock<IEmailSender> _mockEmailSender = new();
    private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepository = new();
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher = new();
    private readonly Mock<IDisplayNameHistoryRepository> _mockDisplayNameHistoryRepository = new();
    private readonly Mock<ITurnstileService> _mockTurnstileService = new();
    private AuthenticationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockTurnstileService.Setup(t => t.VerifyAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(true);

        _useCase = new AuthenticationUseCase(
            _mockUserRepository.Object,
            _mockPasswordHasher.Object,
            _mockEmailSender.Object,
            _mockRefreshTokenRepository.Object,
            _mockEventDispatcher.Object,
            _mockDisplayNameHistoryRepository.Object,
            _mockTurnstileService.Object);
    }

    [Test]
    public async Task FullRegistrationWorkflow_RegisterVerifyLogin_WorksEndToEnd()
    {
        // Arrange
        const string email = "newuser@example.com";
        const string password = "SecurePassword123!";
        const string displayName = "NewUser";
        const string baseUrl = "https://example.com";

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync([]);
        _mockPasswordHasher.Setup(h => h.HashPassword(password))
            .Returns("hashed_password");

        // Step 1: Register
        var registerResult = await _useCase.RegisterWithEmailAsync(email, password, displayName, baseUrl);

        // Assert registration
        await Assert.That(registerResult.IsSuccess).IsTrue();
        var registeredUser = registerResult.Value!;
        await Assert.That(registeredUser.Email).IsEqualTo(email);
        await Assert.That(registeredUser.EmailVerified).IsFalse();
        await Assert.That(registeredUser.EmailVerificationToken).IsNotNull();

        var verificationToken = registeredUser.EmailVerificationToken!;

        // Step 2: Verify email
        _mockUserRepository.Setup(r => r.GetByEmailVerificationTokenAsync(verificationToken))
            .ReturnsAsync(registeredUser);

        var verifyResult = await _useCase.VerifyEmailAsync(verificationToken);

        // Assert verification
        await Assert.That(verifyResult.IsSuccess).IsTrue();
        await Assert.That(registeredUser.EmailVerified).IsTrue();
        await Assert.That(registeredUser.EmailVerificationToken).IsNull();

        // Step 3: Login
        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(registeredUser);
        _mockPasswordHasher.Setup(h => h.VerifyPassword(password, "hashed_password"))
            .Returns(true);

        var loginResult = await _useCase.LoginWithEmailAsync(email, password);

        // Assert login
        await Assert.That(loginResult.IsSuccess).IsTrue();
        await Assert.That(loginResult.Value).IsEqualTo(registeredUser);

        // Verify all expected calls were made
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Exactly(2)); // Verify + Login
        _mockEmailSender.Verify(
            e => e.SendEmailVerificationAsync(email, displayName, It.IsAny<string>(), baseUrl),
            Times.Once);
    }

    [Test]
    public async Task RegistrationWorkflow_WithDuplicateDisplayName_GeneratesUniqueName()
    {
        // Arrange
        const string email = "user@example.com";
        const string password = "Password123!";
        const string displayName = "PopularName";
        const string existingEmail = "existing@example.com";

        var existingUser = User.CreateWithEmail(displayName, existingEmail, "hash", "token");

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync([existingUser]);
        _mockPasswordHasher.Setup(h => h.HashPassword(password))
            .Returns("hash");

        // Act
        var registerResult = await _useCase.RegisterWithEmailAsync(email, password, displayName, "https://example.com");

        // Assert
        await Assert.That(registerResult.IsSuccess).IsTrue();
        await Assert.That(registerResult.Value!.DisplayName).IsNotEqualTo(displayName);
        await Assert.That(registerResult.Value.DisplayName).StartsWith(displayName);
        await Assert.That(registerResult.Value.DisplayName).Matches(@"PopularName-\d+");
    }

    [Test]
    public async Task RegistrationWorkflow_LoginBeforeVerification_SucceedsButShowsUnverified()
    {
        // Arrange
        const string email = "unverified@example.com";
        const string password = "Password123!";

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync([]);
        _mockPasswordHasher.Setup(h => h.HashPassword(password))
            .Returns("hashed_password");

        // Step 1: Register
        var registerResult = await _useCase.RegisterWithEmailAsync(email, password, "User", "https://example.com");
        var user = registerResult.Value!;

        // Step 2: Try to login before verification
        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(user);
        _mockPasswordHasher.Setup(h => h.VerifyPassword(password, "hashed_password"))
            .Returns(true);

        var loginResult = await _useCase.LoginWithEmailAsync(email, password);

        // Assert - Can login but email is not verified
        await Assert.That(loginResult.IsSuccess).IsTrue();
        await Assert.That(loginResult.Value!.EmailVerified).IsFalse();
    }

    [Test]
    public async Task RegistrationWorkflow_DoubleVerification_SecondVerificationFails()
    {
        // Arrange
        const string email = "test@example.com";
        var user = User.CreateWithEmail("TestUser", email, "hash", "verification_token");

        _mockUserRepository.Setup(r => r.GetByEmailVerificationTokenAsync("verification_token"))
            .ReturnsAsync(user);

        // Step 1: First verification (succeeds)
        var firstVerifyResult = await _useCase.VerifyEmailAsync("verification_token");

        await Assert.That(firstVerifyResult.IsSuccess).IsTrue();
        await Assert.That(user.EmailVerified).IsTrue();

        // After verification, the token is set to null, so the repository returns null
        _mockUserRepository.Setup(r => r.GetByEmailVerificationTokenAsync("verification_token"))
            .ReturnsAsync((User?)null);

        // Step 2: Try to verify again
        var secondVerifyResult = await _useCase.VerifyEmailAsync("verification_token");

        // Assert - Second verification should fail
        // After verification succeeds, the token is set to null, so when we try to verify again
        // with the same token, the repository returns null, hence "Invalid or expired verification token"
        await Assert.That(secondVerifyResult.IsSuccess).IsFalse();
        await Assert.That(secondVerifyResult.Error).Contains("Invalid or expired verification token");
    }

    [Test]
    public async Task OAuthRegistrationWorkflow_NewUser_CreatesAndLogsIn()
    {
        // Arrange
        const string oauthProvider = "google";
        const string oauthProviderId = "google_123";
        const string email = "oauth@example.com";
        const string displayName = "OAuthUser";

        _mockUserRepository.Setup(r => r.GetByOAuthProviderIdAsync(oauthProviderId))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync([]);

        // Act
        var loginResult = await _useCase.LoginWithOAuthAsync(oauthProvider, oauthProviderId, email, displayName);

        // Assert
        await Assert.That(loginResult.IsSuccess).IsTrue();
        var user = loginResult.Value!;
        await Assert.That(user.Email).IsEqualTo(email);
        await Assert.That(user.OAuthProvider).IsEqualTo(oauthProvider);
        await Assert.That(user.OAuthProviderId).IsEqualTo(oauthProviderId);
        await Assert.That(user.EmailVerified).IsTrue();
        await Assert.That(user.HasPassword()).IsFalse();

        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _mockEmailSender.Verify(e => e.SendWelcomeEmailAsync(email, It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task OAuthRegistrationWorkflow_ExistingOAuthUser_JustLogsIn()
    {
        // Arrange
        const string oauthProvider = "github";
        const string oauthProviderId = "github_456";
        var existingUser = User.CreateWithOAuth("ExistingUser", "existing@example.com", oauthProvider, oauthProviderId);

        _mockUserRepository.Setup(r => r.GetByOAuthProviderIdAsync(oauthProviderId))
            .ReturnsAsync(existingUser);

        // Act
        var loginResult = await _useCase.LoginWithOAuthAsync(oauthProvider, oauthProviderId, "existing@example.com", "ExistingUser");

        // Assert
        await Assert.That(loginResult.IsSuccess).IsTrue();
        await Assert.That(loginResult.Value).IsEqualTo(existingUser);

        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never); // No new user created
        _mockUserRepository.Verify(r => r.UpdateAsync(existingUser), Times.Once); // LastLogin updated
        _mockEmailSender.Verify(e => e.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task UpdateDisplayNameWorkflow_CheckAvailabilityAndUpdate_Works()
    {
        // Arrange
        var user = User.CreateWithEmail("OldName", "user@example.com", "hash", "token");
        const string newDisplayName = "NewName";

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync([user]); // Only this user exists
        _mockPasswordHasher.Setup(p => p.VerifyPassword("password", "hash")).Returns(true);

        // Act
        var updateResult = await _useCase.UpdateDisplayNameAsync(user.PublicId, newDisplayName, "password", "token");

        // Assert
        await Assert.That(updateResult.IsSuccess).IsTrue();
        await Assert.That(user.DisplayName).IsEqualTo(newDisplayName);
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Test]
    public async Task UpdatePreferencesWorkflow_UpdateMultiplePreferences_Works()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);

        // Act - Update auto-follow preference
        var updateResult = await _useCase.UpdatePreferencesAsync(user.PublicId, autoFollowOnReply: false);

        // Assert
        await Assert.That(updateResult.IsSuccess).IsTrue();
        await Assert.That(user.AutoFollowOnReply).IsFalse();
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }
}
