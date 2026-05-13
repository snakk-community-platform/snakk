namespace Snakk.Application.UseCases;

using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;
using System.Text.RegularExpressions;

public class AuthenticationUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IRefreshTokenRepository refreshTokenRepository,
    IDomainEventDispatcher eventDispatcher,
    IDisplayNameHistoryRepository displayNameHistoryRepository,
    ITurnstileService turnstileService,
    IUserSocialLinkRepository socialLinkRepository,
    DisplayNameValidator displayNameValidator) : UseCaseBase
{
    // Dummy BCrypt hash for timing equalization (prevents email enumeration)
    private static readonly string DummyPasswordHash = "$2a$12$LJ3m4ys3Gy2e1mGFBgHnMeZOp5xDz4MBpUmLhMYkP5K8xA2YUCIi";

    public async Task<Result<User>> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName,
        string baseUrl,
        bool? allowAdultContent = null)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(email))
            return Result<User>.Failure("Email is required");

        var (emailIsValid, emailError) = DisposableEmailValidator.Validate(email);
        if (!emailIsValid)
            return Result<User>.Failure(emailError!);

        if (string.IsNullOrWhiteSpace(password))
            return Result<User>.Failure("Password is required");

        if (password.Length < 8)
            return Result<User>.Failure("Password must be at least 8 characters");

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"))
            return Result<User>.Failure("Password must contain at least one uppercase letter");
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"))
            return Result<User>.Failure("Password must contain at least one lowercase letter");
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"\d"))
            return Result<User>.Failure("Password must contain at least one number");
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            return Result<User>.Failure("Password must contain at least one special character");

        var (dnIsValid, dnError) = await displayNameValidator.ValidateAsync(displayName?.Trim() ?? "");
        if (!dnIsValid)
            return Result<User>.Failure(dnError!);

        // Check if email already exists
        var existingUser = await userRepository.GetByEmailAsync(email);

        if (existingUser is not null)
        {
            // Equalize timing: hash the password even when email is taken
            // to prevent email enumeration via response time differences
            passwordHasher.HashPassword(password);
            return Result<User>.Failure("Email is already registered");
        }

        // Check if display name is available
        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(displayName);

        // Hash password
        var passwordHash = passwordHasher.HashPassword(password);

        // Generate verification token
        var verificationToken = Guid.NewGuid().ToString("N");

        // Create user
        var user = User.CreateWithEmail(
            suggestedDisplayName,
            email,
            passwordHash,
            verificationToken,
            allowAdultContent);

        await userRepository.AddAsync(user);

        // Dispatch domain events
        await eventDispatcher.DispatchAsync(user.DomainEvents);
        user.ClearDomainEvents();

        // Send verification email
        await emailSender.SendEmailVerificationAsync(email, suggestedDisplayName, verificationToken, baseUrl);

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> LoginWithEmailAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result<User>.Failure("Email is required");

        if (string.IsNullOrWhiteSpace(password))
            return Result<User>.Failure("Password is required");

        // Get user by email
        var user = await userRepository.GetByEmailAsync(email);

        if (user is null)
        {
            // Equalize timing: run BCrypt verify even when user doesn't exist
            // to prevent email enumeration via response time differences
            passwordHasher.VerifyPassword(password, DummyPasswordHash);
            return Result<User>.Failure("Invalid email or password");
        }

        // Check account lockout
        if (user.IsLockedOut)
            return Result<User>.Failure("Account is temporarily locked due to too many failed login attempts. Please try again later.");

        // Verify password
        if (!user.HasPassword() || !passwordHasher.VerifyPassword(password, user.PasswordHash!))
        {
            user.RecordFailedLogin(maxAttempts: 5, lockoutMinutes: 15);
            await userRepository.UpdateAsync(user);
            return Result<User>.Failure("Invalid email or password");
        }

        // Update last login (also resets failed attempts + lockout)
        user.UpdateLastLogin();
        await userRepository.UpdateAsync(user);

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> LoginWithOAuthAsync(
        string oauthProvider,
        string oauthProviderId,
        string email,
        string displayName,
        bool? allowAdultContent = null)
    {
        // Try to find existing user by OAuth provider ID
        var user = await userRepository.GetByOAuthProviderIdAsync(oauthProviderId);

        if (user is not null)
        {
            // Existing OAuth user - update last login
            user.UpdateLastLogin();
            await userRepository.UpdateAsync(user);

            return Result<User>.Success(user);
        }

        // Check if email is already registered (link accounts)
        user = await userRepository.GetByEmailAsync(email);

        if (user is not null)
        {
            // Email exists - this could be a security issue
            // For now, don't auto-link - require user to login with password first
            return Result<User>.Failure($"An account with {email} already exists. Please login with your password to link your {oauthProvider} account.");
        }

        // Create new user with OAuth (no display name — set during profile setup)
        var (oauthEmailIsValid, oauthEmailError) = DisposableEmailValidator.Validate(email);
        if (!oauthEmailIsValid)
            return Result<User>.Failure(oauthEmailError!);

        user = User.CreateWithOAuth(
            email,
            oauthProvider,
            oauthProviderId,
            allowAdultContent);

        await userRepository.AddAsync(user);

        // Dispatch domain events
        await eventDispatcher.DispatchAsync(user.DomainEvents);
        user.ClearDomainEvents();

        // Send welcome email
        await emailSender.SendWelcomeEmailAsync(email, user.DisplayName ?? "there");

        return Result<User>.Success(user);
    }

    public async Task<Result> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure("Verification token is required");

        // Find user by token
        var user = await userRepository.GetByEmailVerificationTokenAsync(token);

        if (user is null)
            return Result.Failure("Invalid or expired verification token");

        if (user.EmailVerified)
            return Result.Failure("Email is already verified");

        user.VerifyEmail();
        await userRepository.UpdateAsync(user);

        return Result.Success();
    }

    public async Task<Result<User>> GetUserByIdAsync(UserId userId)
    {
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return Result<User>.Failure("User not found");

        return Result<User>.Success(user);
    }

    public async Task<Result> UpdateDisplayNameAsync(
        UserId userId, string newDisplayName, string? password = null, string? turnstileToken = null)
    {
        var trimmed = newDisplayName?.Trim() ?? "";

        var (isValid, validationError) = await displayNameValidator.ValidateAsync(trimmed);
        if (!isValid)
            return Result.Failure(validationError!);

        var user = await userRepository.GetByPublicIdAsync(userId);
        if (user is null)
            return Result.Failure("User not found");

        // Skip cooldown/password/captcha for initial profile setup
        if (!user.NeedsProfileSetup)
        {
            // Check lock
            if (user.IsDisplayNameLocked)
                return Result.Failure("Your display name has been locked by an administrator.");

            // Check cooldown
            if (!user.CanChangeDisplayName())
            {
                var remaining = user.GetCooldownDaysRemaining();
                return Result.Failure($"You can change your display name again in {remaining} day{(remaining == 1 ? "" : "s")}.");
            }

            // Verify password (required for users with a password)
            if (user.PasswordHash is not null)
            {
                if (string.IsNullOrEmpty(password))
                    return Result.Failure("Password is required to change your display name.");

                if (!passwordHasher.VerifyPassword(password, user.PasswordHash))
                    return Result.Failure("Incorrect password.");
            }

            // Verify Turnstile captcha
            if (!await turnstileService.VerifyAsync(turnstileToken ?? ""))
                return Result.Failure("Captcha verification failed. Please try again.");
        }

        // Check current uniqueness (among active users)
        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(trimmed);
        if (suggestedDisplayName != trimmed)
            return Result.Failure($"Display name '{trimmed}' is taken. Try '{suggestedDisplayName}' instead.");

        // Check historical uniqueness (name was never used by anyone)
        if (await displayNameHistoryRepository.WasNameEverUsedAsync(trimmed))
            return Result.Failure($"The display name '{trimmed}' was previously used and cannot be reused.");

        // Record history before changing
        var previousName = user.DisplayName ?? "";
        user.UpdateDisplayName(trimmed);
        await userRepository.UpdateAsync(user);
        await displayNameHistoryRepository.AddAsync(user.PublicId.Value, previousName, trimmed);

        return Result.Success();
    }

    /// <summary>
    /// Admin: force rename a user, bypassing cooldown/password/captcha.
    /// </summary>
    public async Task<Result> ForceRenameUserAsync(UserId targetUserId, string newDisplayName, UserId adminUserId)
    {
        var trimmed = newDisplayName?.Trim() ?? "";

        var (isValid, validationError) = await displayNameValidator.ValidateAsync(trimmed);
        if (!isValid)
            return Result.Failure(validationError!);

        var user = await userRepository.GetByPublicIdAsync(targetUserId);
        if (user is null)
            return Result.Failure("User not found");

        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(trimmed);
        if (suggestedDisplayName != trimmed)
            return Result.Failure($"Display name '{trimmed}' is taken.");

        var previousName = user.DisplayName ?? "";
        user.UpdateDisplayName(trimmed);
        await userRepository.UpdateAsync(user);
        await displayNameHistoryRepository.AddAsync(
            user.PublicId.Value, previousName, trimmed, adminUserId.Value);

        return Result.Success();
    }

    /// <summary>
    /// Admin: lock/unlock a user's display name.
    /// </summary>
    public async Task<Result> SetDisplayNameLockAsync(UserId targetUserId, bool locked)
    {
        var user = await userRepository.GetByPublicIdAsync(targetUserId);
        if (user is null)
            return Result.Failure("User not found");

        if (locked)
            user.LockDisplayName();
        else
            user.UnlockDisplayName();

        await userRepository.UpdateAsync(user);
        return Result.Success();
    }

    public async Task<Result> UpdatePreferencesAsync(
        UserId userId,
        bool? autoFollowOnReply,
        string? timezone = null,
        string? bio = null,
        bool? allowAdultContent = null,
        bool clearAllowAdultContent = false,
        AdultPreviewImageModeEnum? adultPreviewImageMode = null,
        bool? hidePresence = null)
    {
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return Result.Failure("User not found");

        if (autoFollowOnReply.HasValue)
            user.SetAutoFollowOnReply(autoFollowOnReply.Value);

        if (timezone is not null)
            user.SetTimezone(timezone == "" ? null : timezone);

        if (bio is not null)
            user.SetBio(bio == "" ? null : bio);

        if (clearAllowAdultContent)
            user.SetAllowAdultContent(null);
        else if (allowAdultContent.HasValue)
            user.SetAllowAdultContent(allowAdultContent.Value);

        if (adultPreviewImageMode.HasValue)
            user.SetAdultPreviewImageMode(adultPreviewImageMode.Value);

        if (hidePresence.HasValue)
            user.SetHidePresence(hidePresence.Value);

        await userRepository.UpdateAsync(user);

        return Result.Success();
    }

    public async Task UpdateUserAsync(User user) =>
        await userRepository.UpdateAsync(user);

    private async Task<string> EnsureUniqueDisplayNameAsync(string displayName)
    {
        // Check if display name is available (database-side, case-insensitive)
        var existing = await userRepository.GetByDisplayNameAsync(displayName);

        if (existing is null)
            return displayName;

        // Generate unique display name with random number
        var random = new Random();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suggestedName = $"{displayName}-{random.Next(1000, 9999)}";

            if (await userRepository.GetByDisplayNameAsync(suggestedName) is null)
                return suggestedName;
        }

        // Fallback to GUID
        return $"{displayName}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    public async Task<Result<RefreshToken>> CreateRefreshTokenAsync(UserId userId)
    {
        var refreshToken = RefreshToken.Create(userId, expirationDays: 30);
        await refreshTokenRepository.AddAsync(refreshToken);

        return Result<RefreshToken>.Success(refreshToken);
    }

    public async Task<Result<(User user, RefreshToken newRefreshToken)>> RefreshTokenAsync(string refreshTokenValue)
    {
        var refreshToken = await refreshTokenRepository.GetByValueAsync(refreshTokenValue);

        if (refreshToken is null)
            return Result<(User, RefreshToken)>.Failure("Invalid refresh token");

        if (!refreshToken.IsActive)
            return Result<(User, RefreshToken)>.Failure("Refresh token is expired or revoked");

        var user = await userRepository.GetByPublicIdAsync(refreshToken.UserId);

        if (user is null)
            return Result<(User, RefreshToken)>.Failure("User not found");

        // Revoke old token
        var revokedToken = refreshToken.Revoke();
        await refreshTokenRepository.UpdateAsync(revokedToken);

        // Create new refresh token
        var newRefreshToken = RefreshToken.Create(refreshToken.UserId, expirationDays: 30);
        await refreshTokenRepository.AddAsync(newRefreshToken);

        return Result<(User, RefreshToken)>.Success((user, newRefreshToken));
    }

    public async Task<Result> RevokeRefreshTokensAsync(UserId userId)
    {
        await refreshTokenRepository.RevokeAllForUserAsync(userId);
        return Result.Success();
    }

    // ─── Social Links ────────────────────────────────────────────────────────

    public Task<List<(string Platform, string Username)>> GetMySocialLinksAsync(UserId userId)
        => socialLinkRepository.GetByUserPublicIdAsync(userId.Value);

    public Task<List<(string Platform, string Username)>> GetSocialLinksByPublicIdAsync(string publicId)
        => socialLinkRepository.GetByUserPublicIdAsync(publicId);

    public async Task<Result> UpdateSocialLinksAsync(UserId userId, List<(string Platform, string Value)> rawLinks)
    {
        if (rawLinks.Count > 10)
            return Result.Failure("You can add a maximum of 10 social links.");

        var platforms = rawLinks.Select(l => l.Platform).ToList();
        if (platforms.Count != platforms.Distinct().Count())
            return Result.Failure("Duplicate platforms are not allowed.");

        var normalised = new List<(string Platform, string Username)>();

        foreach (var (platformKey, rawValue) in rawLinks)
        {
            if (!SocialPlatformRegistry.All.TryGetValue(platformKey, out var platform))
                return Result.Failure($"Unknown platform: {platformKey}");

            var username = platform.ParseInput(rawValue ?? "");
            if (username is null)
                return Result.Failure($"Could not parse a valid username for {platform.DisplayName}. Check the format and try again.");

            if (!Regex.IsMatch(username, platform.UsernamePattern))
                return Result.Failure($"Invalid username format for {platform.DisplayName}.");

            normalised.Add((platformKey, username));
        }

        var internalId = await socialLinkRepository.GetUserInternalIdByPublicIdAsync(userId.Value);
        if (internalId is null)
            return Result.Failure("User not found.");

        await socialLinkRepository.ReplaceAllAsync(internalId.Value, normalised);
        return Result.Success();
    }
}
