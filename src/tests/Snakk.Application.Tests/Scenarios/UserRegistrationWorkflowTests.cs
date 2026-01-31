using FluentAssertions;
using Moq;
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
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<IEmailSender> _mockEmailSender;
    private readonly Mock<IRefreshTokenRepository> _mockRefreshTokenRepository;
    private readonly AuthenticationUseCase _useCase;

    public UserRegistrationWorkflowTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockEmailSender = new Mock<IEmailSender>();
        _mockRefreshTokenRepository = new Mock<IRefreshTokenRepository>();

        _useCase = new AuthenticationUseCase(
            _mockUserRepository.Object,
            _mockPasswordHasher.Object,
            _mockEmailSender.Object,
            _mockRefreshTokenRepository.Object);
    }

    [Fact]
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
            .ReturnsAsync(new List<User>());
        _mockPasswordHasher.Setup(h => h.HashPassword(password))
            .Returns("hashed_password");

        // Step 1: Register
        var registerResult = await _useCase.RegisterWithEmailAsync(email, password, displayName, baseUrl);

        // Assert registration
        registerResult.IsSuccess.Should().BeTrue();
        var registeredUser = registerResult.Value!;
        registeredUser.Email.Should().Be(email);
        registeredUser.EmailVerified.Should().BeFalse();
        registeredUser.EmailVerificationToken.Should().NotBeNullOrEmpty();

        var verificationToken = registeredUser.EmailVerificationToken!;

        // Step 2: Verify email
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { registeredUser });

        var verifyResult = await _useCase.VerifyEmailAsync(verificationToken);

        // Assert verification
        verifyResult.IsSuccess.Should().BeTrue();
        registeredUser.EmailVerified.Should().BeTrue();
        registeredUser.EmailVerificationToken.Should().BeNull();

        // Step 3: Login
        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(registeredUser);
        _mockPasswordHasher.Setup(h => h.VerifyPassword(password, "hashed_password"))
            .Returns(true);

        var loginResult = await _useCase.LoginWithEmailAsync(email, password);

        // Assert login
        loginResult.IsSuccess.Should().BeTrue();
        loginResult.Value.Should().Be(registeredUser);

        // Verify all expected calls were made
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Exactly(2)); // Verify + Login
        _mockEmailSender.Verify(
            e => e.SendEmailVerificationAsync(email, displayName, It.IsAny<string>(), baseUrl),
            Times.Once);
    }

    [Fact]
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
            .ReturnsAsync(new List<User> { existingUser });
        _mockPasswordHasher.Setup(h => h.HashPassword(password))
            .Returns("hash");

        // Act
        var registerResult = await _useCase.RegisterWithEmailAsync(email, password, displayName, "https://example.com");

        // Assert
        registerResult.IsSuccess.Should().BeTrue();
        registerResult.Value!.DisplayName.Should().NotBe(displayName);
        registerResult.Value.DisplayName.Should().StartWith(displayName);
        registerResult.Value.DisplayName.Should().MatchRegex(@"PopularName-\d+");
    }

    [Fact]
    public async Task RegistrationWorkflow_LoginBeforeVerification_SucceedsButShowsUnverified()
    {
        // Arrange
        const string email = "unverified@example.com";
        const string password = "Password123!";

        _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User>());
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
        loginResult.IsSuccess.Should().BeTrue();
        loginResult.Value!.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task RegistrationWorkflow_DoubleVerification_SecondVerificationFails()
    {
        // Arrange
        const string email = "test@example.com";
        var user = User.CreateWithEmail("TestUser", email, "hash", "verification_token");

        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { user });

        // Step 1: First verification (succeeds)
        var firstVerifyResult = await _useCase.VerifyEmailAsync("verification_token");

        firstVerifyResult.IsSuccess.Should().BeTrue();
        user.EmailVerified.Should().BeTrue();

        // Step 2: Try to verify again
        var secondVerifyResult = await _useCase.VerifyEmailAsync("verification_token");

        // Assert - Second verification should fail
        // Note: After verification succeeds, the token is set to null, so when we try to verify again
        // with the same token, the user lookup by token returns null, hence "Invalid or expired verification token"
        secondVerifyResult.IsSuccess.Should().BeFalse();
        secondVerifyResult.Error.Should().Contain("Invalid or expired verification token");
    }

    [Fact]
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
            .ReturnsAsync(new List<User>());

        // Act
        var loginResult = await _useCase.LoginWithOAuthAsync(oauthProvider, oauthProviderId, email, displayName);

        // Assert
        loginResult.IsSuccess.Should().BeTrue();
        var user = loginResult.Value!;
        user.Email.Should().Be(email);
        user.OAuthProvider.Should().Be(oauthProvider);
        user.OAuthProviderId.Should().Be(oauthProviderId);
        user.EmailVerified.Should().BeTrue(); // OAuth users are auto-verified
        user.HasPassword().Should().BeFalse();

        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _mockEmailSender.Verify(e => e.SendWelcomeEmailAsync(email, It.IsAny<string>()), Times.Once);
    }

    [Fact]
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
        loginResult.IsSuccess.Should().BeTrue();
        loginResult.Value.Should().Be(existingUser);

        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never); // No new user created
        _mockUserRepository.Verify(r => r.UpdateAsync(existingUser), Times.Once); // LastLogin updated
        _mockEmailSender.Verify(e => e.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDisplayNameWorkflow_CheckAvailabilityAndUpdate_Works()
    {
        // Arrange
        var user = User.CreateWithEmail("OldName", "user@example.com", "hash", "token");
        const string newDisplayName = "NewName";

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User> { user }); // Only this user exists

        // Act
        var updateResult = await _useCase.UpdateDisplayNameAsync(user.PublicId, newDisplayName);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        user.DisplayName.Should().Be(newDisplayName);
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdatePreferencesWorkflow_UpdateMultiplePreferences_Works()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);

        // Act - Update to enable endless scroll
        var updateResult = await _useCase.UpdatePreferencesAsync(user.PublicId, preferEndlessScroll: true);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        user.PreferEndlessScroll.Should().BeTrue();
        _mockUserRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }
}
