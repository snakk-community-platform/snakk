using NSubstitute;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Shared.Enums;

namespace Snakk.Application.Tests.Scenarios;

/// <summary>
/// Comprehensive workflow tests for user registration scenarios
/// </summary>
public class UserRegistrationWorkflowTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IDomainEventDispatcher _eventDispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly IDisplayNameHistoryRepository _displayNameHistoryRepository = Substitute.For<IDisplayNameHistoryRepository>();
    private readonly ITurnstileService _turnstileService = Substitute.For<ITurnstileService>();
    private readonly IUserSocialLinkRepository _socialLinkRepository = Substitute.For<IUserSocialLinkRepository>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private AuthenticationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _turnstileService.VerifyAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
        _settingsService.GetAllowedDisplayNameScriptsAsync()
            .Returns(Task.FromResult<IReadOnlyList<ScriptGroup>>([ScriptGroup.Latin]));

        _useCase = new AuthenticationUseCase(
            _userRepository,
            _passwordHasher,
            _emailSender,
            _refreshTokenRepository,
            _eventDispatcher,
            _displayNameHistoryRepository,
            _turnstileService,
            _socialLinkRepository,
            new DisplayNameValidator(_settingsService));
    }

    [Test]
    public async Task FullRegistrationWorkflow_RegisterVerifyLogin_WorksEndToEnd()
    {
        // Arrange
        const string email = "newuser@example.com";
        const string password = "SecurePassword123!";
        const string displayName = "NewUser";
        const string baseUrl = "https://example.com";

        _userRepository.GetByEmailAsync(email)
            .Returns((User?)null);
        _userRepository.GetAllAsync()
            .Returns([]);
        _passwordHasher.HashPassword(password)
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
        _userRepository.GetByEmailVerificationTokenAsync(verificationToken)
            .Returns(registeredUser);

        var verifyResult = await _useCase.VerifyEmailAsync(verificationToken);

        // Assert verification
        await Assert.That(verifyResult.IsSuccess).IsTrue();
        await Assert.That(registeredUser.EmailVerified).IsTrue();
        await Assert.That(registeredUser.EmailVerificationToken).IsNull();

        // Step 3: Login
        _userRepository.GetByEmailAsync(email)
            .Returns(registeredUser);
        _passwordHasher.VerifyPassword(password, "hashed_password")
            .Returns(true);

        var loginResult = await _useCase.LoginWithEmailAsync(email, password);

        // Assert login
        await Assert.That(loginResult.IsSuccess).IsTrue();
        await Assert.That(loginResult.Value).IsEqualTo(registeredUser);

        // Verify all expected calls were made
        await _userRepository.Received(1).AddAsync(Arg.Any<User>());
        await _userRepository.Received(2).UpdateAsync(Arg.Any<User>()); // Verify + Login
        await _emailSender.Received(1).SendEmailVerificationAsync(email, displayName, Arg.Any<string>(), baseUrl);
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

        _userRepository.GetByEmailAsync(email)
            .Returns((User?)null);
        _userRepository.GetByDisplayNameAsync(displayName)
            .Returns(existingUser);
        _userRepository.GetByDisplayNameAsync(Arg.Is<string>(n => n != displayName))
            .Returns((User?)null);
        _passwordHasher.HashPassword(password)
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

        _userRepository.GetByEmailAsync(email)
            .Returns((User?)null);
        _userRepository.GetAllAsync()
            .Returns([]);
        _passwordHasher.HashPassword(password)
            .Returns("hashed_password");

        // Step 1: Register
        var registerResult = await _useCase.RegisterWithEmailAsync(email, password, "User", "https://example.com");
        var user = registerResult.Value!;

        // Step 2: Try to login before verification
        _userRepository.GetByEmailAsync(email)
            .Returns(user);
        _passwordHasher.VerifyPassword(password, "hashed_password")
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

        _userRepository.GetByEmailVerificationTokenAsync("verification_token")
            .Returns(user);

        // Step 1: First verification (succeeds)
        var firstVerifyResult = await _useCase.VerifyEmailAsync("verification_token");

        await Assert.That(firstVerifyResult.IsSuccess).IsTrue();
        await Assert.That(user.EmailVerified).IsTrue();

        // After verification, the token is set to null, so the repository returns null
        _userRepository.GetByEmailVerificationTokenAsync("verification_token")
            .Returns((User?)null);

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

        _userRepository.GetByOAuthProviderIdAsync(oauthProviderId)
            .Returns((User?)null);
        _userRepository.GetByEmailAsync(email)
            .Returns((User?)null);
        _userRepository.GetAllAsync()
            .Returns([]);

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

        await _userRepository.Received(1).AddAsync(Arg.Any<User>());
        await _emailSender.Received(1).SendWelcomeEmailAsync(email, Arg.Any<string>());
    }

    [Test]
    public async Task OAuthRegistrationWorkflow_ExistingOAuthUser_JustLogsIn()
    {
        // Arrange
        const string oauthProvider = "github";
        const string oauthProviderId = "github_456";
        var existingUser = User.CreateWithOAuth("existing@example.com", oauthProvider, oauthProviderId);

        _userRepository.GetByOAuthProviderIdAsync(oauthProviderId)
            .Returns(existingUser);

        // Act
        var loginResult = await _useCase.LoginWithOAuthAsync(oauthProvider, oauthProviderId, "existing@example.com", "ExistingUser");

        // Assert
        await Assert.That(loginResult.IsSuccess).IsTrue();
        await Assert.That(loginResult.Value).IsEqualTo(existingUser);

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>()); // No new user created
        await _userRepository.Received(1).UpdateAsync(existingUser); // LastLogin updated
        await _emailSender.DidNotReceive().SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task UpdateDisplayNameWorkflow_CheckAvailabilityAndUpdate_Works()
    {
        // Arrange
        var user = User.CreateWithEmail("OldName", "user@example.com", "hash", "token");
        const string newDisplayName = "NewName";

        _userRepository.GetByPublicIdAsync(user.PublicId)
            .Returns(user);
        _userRepository.GetAllAsync()
            .Returns([user]); // Only this user exists
        _passwordHasher.VerifyPassword("password", "hash").Returns(true);

        // Act
        var updateResult = await _useCase.UpdateDisplayNameAsync(user.PublicId, newDisplayName, "password", "token");

        // Assert
        await Assert.That(updateResult.IsSuccess).IsTrue();
        await Assert.That(user.DisplayName).IsEqualTo(newDisplayName);
        await _userRepository.Received(1).UpdateAsync(user);
    }

    [Test]
    public async Task UpdatePreferencesWorkflow_UpdateMultiplePreferences_Works()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _userRepository.GetByPublicIdAsync(user.PublicId)
            .Returns(user);

        // Act - Update auto-follow preference
        var updateResult = await _useCase.UpdatePreferencesAsync(user.PublicId, autoFollowOnReply: false);

        // Assert
        await Assert.That(updateResult.IsSuccess).IsTrue();
        await Assert.That(user.AutoFollowOnReply).IsFalse();
        await _userRepository.Received(1).UpdateAsync(user);
    }
}
