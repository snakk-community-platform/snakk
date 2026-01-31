namespace Snakk.Application.UseCases;

using Snakk.Application.Services;
using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class AuthenticationUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IRefreshTokenRepository refreshTokenRepository) : UseCaseBase
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

    public async Task<Result<User>> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName,
        string baseUrl)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(email))
            return Result<User>.Failure("Email is required");

        if (string.IsNullOrWhiteSpace(password))
            return Result<User>.Failure("Password is required");

        if (password.Length < 8)
            return Result<User>.Failure("Password must be at least 8 characters");

        if (string.IsNullOrWhiteSpace(displayName))
            return Result<User>.Failure("Display name is required");

        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(email);
        if (existingUser != null)
            return Result<User>.Failure("Email is already registered");

        // Check if display name is available
        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(displayName);

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(password);

        // Generate verification token
        var verificationToken = Guid.NewGuid().ToString("N");

        // Create user
        var user = User.CreateWithEmail(
            suggestedDisplayName,
            email,
            passwordHash,
            verificationToken);

        await _userRepository.AddAsync(user);

        // Send verification email
        await _emailSender.SendEmailVerificationAsync(email, suggestedDisplayName, verificationToken, baseUrl);

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> LoginWithEmailAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result<User>.Failure("Email is required");

        if (string.IsNullOrWhiteSpace(password))
            return Result<User>.Failure("Password is required");

        // Get user by email
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return Result<User>.Failure("Invalid email or password");

        // Verify password
        if (!user.HasPassword() || !_passwordHasher.VerifyPassword(password, user.PasswordHash!))
            return Result<User>.Failure("Invalid email or password");

        // Update last login
        user.UpdateLastLogin();
        await _userRepository.UpdateAsync(user);

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> LoginWithOAuthAsync(
        string oauthProvider,
        string oauthProviderId,
        string email,
        string displayName)
    {
        // Try to find existing user by OAuth provider ID
        var user = await _userRepository.GetByOAuthProviderIdAsync(oauthProviderId);

        if (user != null)
        {
            // Existing OAuth user - update last login
            user.UpdateLastLogin();
            await _userRepository.UpdateAsync(user);
            return Result<User>.Success(user);
        }

        // Check if email is already registered (link accounts)
        user = await _userRepository.GetByEmailAsync(email);

        if (user != null)
        {
            // Email exists - this could be a security issue
            // For now, don't auto-link - require user to login with password first
            return Result<User>.Failure($"An account with {email} already exists. Please login with your password to link your {oauthProvider} account.");
        }

        // Create new user with OAuth
        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(displayName);

        user = User.CreateWithOAuth(
            suggestedDisplayName,
            email,
            oauthProvider,
            oauthProviderId);

        await _userRepository.AddAsync(user);

        // Send welcome email
        await _emailSender.SendWelcomeEmailAsync(email, suggestedDisplayName);

        return Result<User>.Success(user);
    }

    public async Task<Result> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure("Verification token is required");

        // Find user by token (need to add method to repository)
        var users = await _userRepository.GetAllAsync();
        var user = users.FirstOrDefault(u => u.EmailVerificationToken == token);

        if (user == null)
            return Result.Failure("Invalid or expired verification token");

        if (user.EmailVerified)
            return Result.Failure("Email is already verified");

        user.VerifyEmail();
        await _userRepository.UpdateAsync(user);

        return Result.Success();
    }

    public async Task<Result<User>> GetUserByIdAsync(UserId userId)
    {
        var user = await _userRepository.GetByPublicIdAsync(userId);
        if (user == null)
            return Result<User>.Failure("User not found");

        return Result<User>.Success(user);
    }

    public async Task<Result> UpdateDisplayNameAsync(UserId userId, string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
            return Result.Failure("Display name cannot be empty");

        var user = await _userRepository.GetByPublicIdAsync(userId);
        if (user == null)
            return Result.Failure("User not found");

        // Check if display name is available
        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(newDisplayName);

        if (suggestedDisplayName != newDisplayName)
            return Result.Failure($"Display name '{newDisplayName}' is taken. Try '{suggestedDisplayName}' instead.");

        user.UpdateDisplayName(newDisplayName);
        await _userRepository.UpdateAsync(user);

        return Result.Success();
    }

    public async Task<Result> UpdatePreferencesAsync(UserId userId, bool? preferEndlessScroll)
    {
        var user = await _userRepository.GetByPublicIdAsync(userId);
        if (user == null)
            return Result.Failure("User not found");

        if (preferEndlessScroll.HasValue)
            user.SetPreferEndlessScroll(preferEndlessScroll.Value);

        await _userRepository.UpdateAsync(user);

        return Result.Success();
    }

    private async Task<string> EnsureUniqueDisplayNameAsync(string displayName)
    {
        // Check if display name is available (case-insensitive)
        var users = await _userRepository.GetAllAsync();
        var existingUser = users.FirstOrDefault(u =>
            u.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

        if (existingUser == null)
            return displayName;

        // Generate unique display name with random number
        var random = new Random();
        var attempt = 0;

        while (attempt < 10)
        {
            var suffix = random.Next(1000, 9999);
            var suggestedName = $"{displayName}-{suffix}";

            existingUser = users.FirstOrDefault(u =>
                u.DisplayName.Equals(suggestedName, StringComparison.OrdinalIgnoreCase));

            if (existingUser == null)
                return suggestedName;

            attempt++;
        }

        // Fallback to GUID
        return $"{displayName}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }

    public async Task<Result<RefreshToken>> CreateRefreshTokenAsync(UserId userId)
    {
        var refreshToken = RefreshToken.Create(userId, expirationDays: 30);
        await _refreshTokenRepository.AddAsync(refreshToken);
        return Result<RefreshToken>.Success(refreshToken);
    }

    public async Task<Result<(User user, RefreshToken newRefreshToken)>> RefreshTokenAsync(string refreshTokenValue)
    {
        var refreshToken = await _refreshTokenRepository.GetByValueAsync(refreshTokenValue);

        if (refreshToken == null)
            return Result<(User, RefreshToken)>.Failure("Invalid refresh token");

        if (!refreshToken.IsActive)
            return Result<(User, RefreshToken)>.Failure("Refresh token is expired or revoked");

        var user = await _userRepository.GetByPublicIdAsync(refreshToken.UserId);
        if (user == null)
            return Result<(User, RefreshToken)>.Failure("User not found");

        // Revoke old token
        var revokedToken = refreshToken.Revoke();
        await _refreshTokenRepository.UpdateAsync(revokedToken);

        // Create new refresh token
        var newRefreshToken = RefreshToken.Create(refreshToken.UserId, expirationDays: 30);
        await _refreshTokenRepository.AddAsync(newRefreshToken);

        return Result<(User, RefreshToken)>.Success((user, newRefreshToken));
    }

    public async Task<Result> RevokeRefreshTokensAsync(UserId userId)
    {
        await _refreshTokenRepository.RevokeAllForUserAsync(userId);
        return Result.Success();
    }
}
