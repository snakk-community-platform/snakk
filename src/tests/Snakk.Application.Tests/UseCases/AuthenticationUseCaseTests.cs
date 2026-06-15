using NSubstitute;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Application.Tests.UseCases;

public class AuthenticationUseCaseTests
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
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordResetRequestRepository _passwordResetRequestRepository = Substitute.For<IPasswordResetRequestRepository>();
    private readonly IAuthVersionCache _authVersionCache = Substitute.For<IAuthVersionCache>();
    private readonly ISlugService _slugService = Substitute.For<ISlugService>();
    private AuthenticationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _turnstileService.VerifyAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
        _settingsService.GetAllowedDisplayNameScriptsAsync()
            .Returns(Task.FromResult<IReadOnlyList<ScriptGroup>>([ScriptGroup.Latin]));

        _useCase = new AuthenticationUseCase(
            _userRepository, _passwordHasher, _emailSender, _refreshTokenRepository,
            _eventDispatcher, _displayNameHistoryRepository, _turnstileService, _socialLinkRepository,
            new DisplayNameValidator(_settingsService), _passwordResetTokenRepository,
            _passwordResetRequestRepository, _authVersionCache, _slugService);
    }

    #region RegisterWithEmailAsync Tests

    [Test]
    public async Task RegisterWithEmailAsync_WithValidParameters_CreatesUser()
    {
        const string email = "test@example.com";
        const string password = "Password123!";
        const string displayName = "TestUser";
        const string baseUrl = "https://example.com";
        const string passwordHash = "hashed_password";

        _userRepository.GetByEmailAsync(email).Returns((User?)null);
        _userRepository.GetAllAsync().Returns([]);
        _passwordHasher.HashPassword(password).Returns(passwordHash);

        var result = await _useCase.RegisterWithEmailAsync(email, password, displayName, baseUrl);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Email).IsEqualTo(email);
        await Assert.That(result.Value.DisplayName).IsEqualTo(displayName);
        await Assert.That(result.Value.EmailVerified).IsFalse();

        await _userRepository.Received(1).AddAsync(Arg.Any<User>());
        await _emailSender.Received(1).SendEmailVerificationAsync(email, displayName, Arg.Any<string>(), baseUrl);
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithEmptyEmail_ReturnsFailure()
    {
        var result = await _useCase.RegisterWithEmailAsync("", "Password123!", "TestUser", "https://example.com");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithNullEmail_ReturnsFailure()
    {
        var result = await _useCase.RegisterWithEmailAsync(null!, "Password123!", "TestUser", "https://example.com");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithWhitespaceEmail_ReturnsFailure()
    {
        var result = await _useCase.RegisterWithEmailAsync("   ", "Password123!", "TestUser", "https://example.com");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithEmptyPassword_ReturnsFailure()
    {
        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "", "TestUser", "https://example.com");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Password is required");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithPasswordLessThan8Characters_ReturnsFailure()
    {
        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "Pass123", "TestUser", "https://example.com");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Password must be at least 8 characters");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithExactly8CharacterPassword_Succeeds()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);
        _userRepository.GetAllAsync().Returns([]);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "Pass123!", "TestUser", "https://example.com");

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithEmptyDisplayName_ReturnsFailure()
    {
        var result = await _useCase.RegisterWithEmailAsync("test@example.com", "Password123!", "", "https://example.com");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Display name cannot be empty");
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithExistingEmail_ReturnsFailure()
    {
        const string email = "existing@example.com";
        var existingUser = User.CreateWithEmail("ExistingUser", email, "hash", "token");
        _userRepository.GetByEmailAsync(email).Returns(existingUser);

        var result = await _useCase.RegisterWithEmailAsync(email, "Password123!", "NewUser", "https://example.com");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is already registered");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Test]
    public async Task RegisterWithEmailAsync_WithTakenDisplayName_GeneratesUniqueDisplayName()
    {
        const string displayName = "TestUser";
        const string email = "new@example.com";
        var existingUser = User.CreateWithEmail(displayName, "existing@example.com", "hash", "token");

        _userRepository.GetByEmailAsync(email).Returns((User?)null);
        _userRepository.GetByDisplayNameAsync(displayName).Returns(existingUser);
        _userRepository.GetByDisplayNameAsync(Arg.Is<string>(n => n != displayName)).Returns((User?)null);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        var result = await _useCase.RegisterWithEmailAsync(email, "Password123!", displayName, "https://example.com");

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
        const string email = "test@example.com";
        const string password = "Password123!";
        const string passwordHash = "hashed_password";
        var user = User.CreateWithEmail("TestUser", email, passwordHash, "token");

        _userRepository.GetByEmailAsync(email).Returns(user);
        _passwordHasher.VerifyPassword(password, passwordHash).Returns(true);

        var result = await _useCase.LoginWithEmailAsync(email, password);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Email).IsEqualTo(email);
        await _userRepository.Received(1).UpdateAsync(Arg.Any<User>());
    }

    [Test]
    public async Task LoginWithEmailAsync_WithEmptyEmail_ReturnsFailure()
    {
        var result = await _useCase.LoginWithEmailAsync("", "Password123!");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Email is required");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithEmptyPassword_ReturnsFailure()
    {
        var result = await _useCase.LoginWithEmailAsync("test@example.com", "");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Password is required");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithNonExistentEmail_ReturnsFailure()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        var result = await _useCase.LoginWithEmailAsync("nonexistent@example.com", "Password123!");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid email or password");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithInvalidPassword_ReturnsFailure()
    {
        const string email = "test@example.com";
        var user = User.CreateWithEmail("TestUser", email, "correct_hash", "token");
        _userRepository.GetByEmailAsync(email).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var result = await _useCase.LoginWithEmailAsync(email, "WrongPassword");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid email or password");
    }

    [Test]
    public async Task LoginWithEmailAsync_WithOAuthUser_ReturnsFailure()
    {
        const string email = "oauth@example.com";
        var oauthUser = User.CreateWithOAuth(email);
        _userRepository.GetByEmailAsync(email).Returns(oauthUser);

        var result = await _useCase.LoginWithEmailAsync(email, "AnyPassword");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid email or password");
    }

    #endregion

    #region LoginWithOAuthAsync Tests

    [Test]
    public async Task LoginWithOAuthAsync_WithExistingOAuthUser_ReturnsUser()
    {
        const string oauthProviderId = "google123";
        var existingUser = User.CreateWithOAuth("test@example.com");
        var connection = UserOAuthConnection.Create(existingUser.PublicId.Value, "google", oauthProviderId);
        _userRepository.GetByOAuthConnectionAsync("google", oauthProviderId, Arg.Any<CancellationToken>()).Returns(existingUser);
        _userRepository.GetOAuthConnectionsAsync(existingUser.PublicId.Value, Arg.Any<CancellationToken>()).Returns([connection]);

        var result = await _useCase.LoginWithOAuthAsync("google", oauthProviderId, "test@example.com", "TestUser");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(existingUser);
        await _userRepository.Received(1).UpdateAsync(Arg.Any<User>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Test]
    public async Task LoginWithOAuthAsync_WithNewOAuthUser_CreatesUser()
    {
        const string email = "newuser@example.com";
        const string displayName = "NewUser";
        const string oauthProvider = "google";
        const string oauthProviderId = "google456";

        _userRepository.GetByOAuthConnectionAsync(oauthProvider, oauthProviderId, Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.GetByEmailAsync(email).Returns((User?)null);
        _userRepository.GetAllAsync().Returns([]);

        var result = await _useCase.LoginWithOAuthAsync(oauthProvider, oauthProviderId, email, displayName);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Email).IsEqualTo(email);
        await Assert.That(result.Value.EmailVerified).IsTrue();
        await _userRepository.Received(1).AddAsync(Arg.Any<User>());
        await _userRepository.Received(1).AddOAuthConnectionAsync(Arg.Any<UserOAuthConnection>());
        await _emailSender.Received(1).SendWelcomeEmailAsync(email, Arg.Any<string>());
    }

    [Test]
    public async Task LoginWithOAuthAsync_WithExistingEmail_ReturnsFailure()
    {
        const string email = "existing@example.com";
        var existingEmailUser = User.CreateWithEmail("ExistingUser", email, "hash", "token");
        _userRepository.GetByOAuthConnectionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.GetByEmailAsync(email).Returns(existingEmailUser);

        var result = await _useCase.LoginWithOAuthAsync("google", "google789", email, "DisplayName");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("already exists");
        await Assert.That(result.Error).Contains("login with your password");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    #endregion

    #region VerifyEmailAsync Tests

    [Test]
    public async Task VerifyEmailAsync_WithValidToken_VerifiesEmail()
    {
        const string token = "valid_token_123";
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", token);
        _userRepository.GetByEmailVerificationTokenAsync(token).Returns(user);

        var result = await _useCase.VerifyEmailAsync(token);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.EmailVerified).IsTrue();
        await _userRepository.Received(1).UpdateAsync(user);
    }

    [Test]
    public async Task VerifyEmailAsync_WithEmptyToken_ReturnsFailure()
    {
        var result = await _useCase.VerifyEmailAsync("");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Verification token is required");
    }

    [Test]
    public async Task VerifyEmailAsync_WithInvalidToken_ReturnsFailure()
    {
        _userRepository.GetByEmailVerificationTokenAsync("invalid_token").Returns((User?)null);

        var result = await _useCase.VerifyEmailAsync("invalid_token");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid or expired verification token");
    }

    [Test]
    public async Task VerifyEmailAsync_WhenAlreadyVerified_ReturnsFailure()
    {
        const string token = "token_123";
        _userRepository.GetByEmailVerificationTokenAsync(token).Returns((User?)null);

        var result = await _useCase.VerifyEmailAsync(token);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Invalid or expired verification token");
    }

    #endregion

    #region UpdateDisplayNameAsync Tests

    [Test]
    public async Task UpdateDisplayNameAsync_WithAvailableName_UpdatesDisplayName()
    {
        var userId = UserId.New();
        var user = User.CreateWithEmail("OldName", "test@example.com", "hash", "token");
        const string newDisplayName = "NewName";

        _userRepository.GetByPublicIdAsync(userId).Returns(user);
        _userRepository.GetAllAsync().Returns([user]);
        _passwordHasher.VerifyPassword("password", "hash").Returns(true);

        var result = await _useCase.UpdateDisplayNameAsync(userId, newDisplayName, "password", "token");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.DisplayName).IsEqualTo(newDisplayName);
        await _userRepository.Received(1).UpdateAsync(user);
    }

    [Test]
    public async Task UpdateDisplayNameAsync_WithTakenName_ReturnsFailure()
    {
        var userId = UserId.New();
        var user = User.CreateWithEmail("CurrentName", "test@example.com", "hash", "token");
        var otherUser = User.CreateWithEmail("TakenName", "other@example.com", "hash", "token");

        _userRepository.GetByPublicIdAsync(userId).Returns(user);
        _userRepository.GetByDisplayNameAsync("TakenName").Returns(otherUser);
        _passwordHasher.VerifyPassword("password", "hash").Returns(true);

        var result = await _useCase.UpdateDisplayNameAsync(userId, "TakenName", "password", "token");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("is taken");
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Test]
    public async Task UpdateDisplayNameAsync_WithEmptyName_ReturnsFailure()
    {
        var userId = UserId.New();

        var result = await _useCase.UpdateDisplayNameAsync(userId, "");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Display name cannot be empty");
    }

    [Test]
    public async Task UpdateDisplayNameAsync_WithNonExistentUser_ReturnsFailure()
    {
        var userId = UserId.New();
        _userRepository.GetByPublicIdAsync(userId).Returns((User?)null);

        var result = await _useCase.UpdateDisplayNameAsync(userId, "NewName");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion

    #region UpdatePreferencesAsync Tests

    [Test]
    public async Task UpdatePreferencesAsync_WithValidPreference_UpdatesUser()
    {
        var userId = UserId.New();
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.UpdatePreferencesAsync(userId, autoFollowOnReply: false);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.AutoFollowOnReply).IsFalse();
        await _userRepository.Received(1).UpdateAsync(user);
    }

    [Test]
    public async Task UpdatePreferencesAsync_WithNullPreference_DoesNotUpdateAnything()
    {
        var userId = UserId.New();
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var originalPreference = user.AutoFollowOnReply;
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.UpdatePreferencesAsync(userId, autoFollowOnReply: null);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(user.AutoFollowOnReply).IsEqualTo(originalPreference);
        await _userRepository.Received(1).UpdateAsync(user);
    }

    [Test]
    public async Task UpdatePreferencesAsync_WithNonExistentUser_ReturnsFailure()
    {
        var userId = UserId.New();
        _userRepository.GetByPublicIdAsync(userId).Returns((User?)null);

        var result = await _useCase.UpdatePreferencesAsync(userId, autoFollowOnReply: true);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion

    #region GetUserByIdAsync Tests

    [Test]
    public async Task GetUserByIdAsync_WithExistingUser_ReturnsUser()
    {
        var userId = UserId.New();
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.GetUserByIdAsync(userId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(user);
    }

    [Test]
    public async Task GetUserByIdAsync_WithNonExistentUser_ReturnsFailure()
    {
        var userId = UserId.New();
        _userRepository.GetByPublicIdAsync(userId).Returns((User?)null);

        var result = await _useCase.GetUserByIdAsync(userId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion
}
