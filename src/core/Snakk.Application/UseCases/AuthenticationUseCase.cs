namespace Snakk.Application.UseCases;

using Snakk.Application.DTOs.Auth;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;
using System.Security.Cryptography;
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
    DisplayNameValidator displayNameValidator,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetRequestRepository passwordResetRequestRepository,
    IAuthVersionCache authVersionCache,
    ISlugService slugService) : UseCaseBase
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
        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(displayName!);

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

        // Assign initial slug from display name
        await slugService.AssignInitialSlugAsync(user.PublicId.Value, suggestedDisplayName);

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

        // Verify password BEFORE checking lockout — equalizes timing so a locked-out
        // account doesn't return faster than one with a wrong password (timing oracle).
        var passwordValid = user.HasPassword() && passwordHasher.VerifyPassword(password, user.PasswordHash!);

        if (user.IsLockedOut)
            return Result<User>.Failure("Account is temporarily locked due to too many failed login attempts. Please try again later.");

        if (!passwordValid)
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
        var provider = oauthProvider.ToLowerInvariant();

        // Try to find existing user by connection table
        var user = await userRepository.GetByOAuthConnectionAsync(provider, oauthProviderId);

        if (user is not null)
        {
            var connections = await userRepository.GetOAuthConnectionsAsync(user.PublicId.Value);
            var conn = connections.FirstOrDefault(c => c.Provider == provider);
            if (conn is not null)
            {
                conn.RecordLogin();
                await userRepository.UpdateOAuthConnectionAsync(conn);
            }
            user.UpdateLastLogin();
            await userRepository.UpdateAsync(user);
            return Result<User>.Success(user);
        }

        // Email collision guard — don't auto-link; require explicit connect flow
        user = await userRepository.GetByEmailAsync(email);
        if (user is not null)
            return Result<User>.Failure($"An account with {email} already exists. Please login with your password to link your {oauthProvider} account.");

        // Validate email before creating account
        var (oauthEmailIsValid, oauthEmailError) = DisposableEmailValidator.Validate(email);
        if (!oauthEmailIsValid)
            return Result<User>.Failure(oauthEmailError!);

        user = User.CreateWithOAuth(email, allowAdultContent);
        await userRepository.AddAsync(user);

        var connection = UserOAuthConnection.Create(user.PublicId.Value, provider, oauthProviderId);
        await userRepository.AddOAuthConnectionAsync(connection);

        await eventDispatcher.DispatchAsync(user.DomainEvents);
        user.ClearDomainEvents();

        await emailSender.SendWelcomeEmailAsync(email, user.DisplayName ?? "there");

        return Result<User>.Success(user);
    }

    public async Task<Result<List<OAuthConnectionDto>>> GetOAuthConnectionsAsync(string userPublicId, CancellationToken ct = default)
    {
        var connections = await userRepository.GetOAuthConnectionsAsync(userPublicId, ct);
        var dtos = connections.Select(c => new OAuthConnectionDto(
            c.Provider, c.ConnectedAt, c.LastLoginAt, c.Require2FA)).ToList();
        return Result<List<OAuthConnectionDto>>.Success(dtos);
    }

    public async Task<Result> ConnectOAuthProviderAsync(
        string userPublicId, string provider, string providerUserId, CancellationToken ct = default)
    {
        var normalized = provider.ToLowerInvariant();

        if (await userRepository.OAuthConnectionExistsAsync(normalized, providerUserId, ct))
            return Result.Failure("PROVIDER_TAKEN_BY_OTHER");

        var existing = await userRepository.GetOAuthConnectionsAsync(userPublicId, ct);
        if (existing.Any(c => c.Provider == normalized))
            return Result.Failure("ALREADY_CONNECTED");

        var connection = UserOAuthConnection.Create(userPublicId, normalized, providerUserId);
        await userRepository.AddOAuthConnectionAsync(connection, ct);
        return Result.Success();
    }

    public async Task<Result> DisconnectOAuthProviderAsync(
        string userPublicId, string provider, CancellationToken ct = default)
    {
        var normalized = provider.ToLowerInvariant();

        var user = await userRepository.GetByPublicIdAsync(UserId.From(userPublicId), ct);
        if (user is null)
            return Result.Failure("User not found.");

        var connections = await userRepository.GetOAuthConnectionsAsync(userPublicId, ct);
        var remainingConnections = connections.Count(c => c.Provider != normalized);

        if (!user.HasPassword() && remainingConnections == 0)
            return Result.Failure("LAST_AUTH_METHOD");

        await userRepository.RemoveOAuthConnectionAsync(userPublicId, normalized, ct);
        return Result.Success();
    }

    public async Task<Result> SetOAuthConnectionRequire2FAAsync(
        string userPublicId, string provider, bool require, CancellationToken ct = default)
    {
        var normalized = provider.ToLowerInvariant();

        if (normalized == "discord")
            return Result.Failure("Discord always requires 2FA and cannot be configured.");

        var connections = await userRepository.GetOAuthConnectionsAsync(userPublicId, ct);
        var conn = connections.FirstOrDefault(c => c.Provider == normalized);
        if (conn is null)
            return Result.Failure("Connection not found.");

        conn.SetRequire2FA(require);
        await userRepository.UpdateOAuthConnectionAsync(conn, ct);
        return Result.Success();
    }

    public async Task<Result> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure("Verification token is required");

        // Find user by token
        var user = await userRepository.GetByEmailVerificationTokenAsync(token);

        if (user is null)
            return Result.Failure("Invalid or expired verification token");

        if (user.EmailVerificationTokenCreatedAt.HasValue &&
            DateTime.UtcNow - user.EmailVerificationTokenCreatedAt.Value > TimeSpan.FromHours(48))
            return Result.Failure("Verification link has expired. Please request a new one.");

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
        UserId userId, string newDisplayName, string? password = null, string? turnstileToken = null, bool sudoVerified = false)
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

            if (!sudoVerified)
            {
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
        }

        // Check current uniqueness (among active users)
        var suggestedDisplayName = await EnsureUniqueDisplayNameAsync(trimmed);
        if (suggestedDisplayName != trimmed)
            return Result.Failure($"Display name '{trimmed}' is taken. Try '{suggestedDisplayName}' instead.");

        // Check historical uniqueness (name was never used by anyone else)
        if (await displayNameHistoryRepository.WasNameEverUsedAsync(trimmed, user.PublicId.Value))
            return Result.Failure($"The display name '{trimmed}' was previously used and cannot be reused.");

        // Record history before changing
        var previousName = user.DisplayName ?? "";
        user.UpdateDisplayName(trimmed);
        await userRepository.UpdateAsync(user);
        await displayNameHistoryRepository.AddAsync(user.PublicId.Value, previousName, trimmed);

        // Auto-update slug from new display name (no-ops if user has a custom slug)
        await slugService.AutoUpdateSlugFromDisplayNameAsync(user.PublicId.Value, trimmed);

        return Result.Success();
    }

    public async Task<Result> SetEmailAsync(UserId userId, string email, CancellationToken ct = default)
    {
        var trimmed = email?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            return Result.Failure("Email is required");

        var existing = await userRepository.GetByEmailAsync(trimmed, ct);
        if (existing is not null && existing.PublicId != userId)
            return Result.Failure("EMAIL_TAKEN");

        var user = await userRepository.GetByPublicIdAsync(userId, ct);
        if (user is null)
            return Result.Failure("User not found");

        if (!string.IsNullOrEmpty(user.Email))
            return Result.Failure("EMAIL_ALREADY_SET");

        user.SetEmail(trimmed);
        await userRepository.UpdateAsync(user, ct);
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

        await slugService.AutoUpdateSlugFromDisplayNameAsync(user.PublicId.Value, trimmed);

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

        if (refreshToken.IsRevoked)
        {
            // Reuse of an already-rotated/revoked token signals token theft: revoke the
            // user's entire token family so a stolen token cannot keep a session alive.
            // A short grace window avoids killing all sessions on benign multi-tab races
            // where an in-flight refresh still carries the just-rotated token.
            var revokedAt = DateTime.Parse(refreshToken.RevokedAt!, null, System.Globalization.DateTimeStyles.RoundtripKind);
            if (DateTime.UtcNow - revokedAt > TimeSpan.FromSeconds(60))
                await refreshTokenRepository.RevokeAllForUserAsync(refreshToken.UserId);

            return Result<(User, RefreshToken)>.Failure("Refresh token is expired or revoked");
        }

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

    public async Task<Result<(User user, RefreshToken newRefreshToken)>> ReloginWithRefreshTokenAsync(
        string refreshTokenValue, string password)
    {
        var refreshToken = await refreshTokenRepository.GetByValueAsync(refreshTokenValue);

        if (refreshToken is null || !refreshToken.IsActive)
            return Result<(User, RefreshToken)>.Failure("Invalid refresh token");

        var user = await userRepository.GetByPublicIdAsync(refreshToken.UserId);
        if (user is null)
            return Result<(User, RefreshToken)>.Failure("User not found");

        if (!user.HasPassword() || !passwordHasher.VerifyPassword(password, user.PasswordHash!))
            return Result<(User, RefreshToken)>.Failure("Incorrect password");

        var revokedToken = refreshToken.Revoke();
        await refreshTokenRepository.UpdateAsync(revokedToken);

        var newRefreshToken = RefreshToken.Create(refreshToken.UserId, expirationDays: 30);
        await refreshTokenRepository.AddAsync(newRefreshToken);

        return Result<(User, RefreshToken)>.Success((user, newRefreshToken));
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

    public async Task<Result> RequestPasswordResetAsync(
        string email, string baseUrl, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var emailHash = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant().Trim())));

        var requestCount = await passwordResetRequestRepository.CountByIpInWindowAsync(
            ipAddress ?? "", TimeSpan.FromHours(1), ct);

        if (requestCount >= 5)
        {
            await passwordResetRequestRepository.LogAsync(
                emailHash, ipAddress ?? "", PasswordResetRequestOutcomeEnum.RateLimited, ct);
            return Result.Success(); // Never reveal rate limiting to callers
        }

        var user = await userRepository.GetByEmailAsync(email, ct);

        if (user is null)
        {
            await passwordResetRequestRepository.LogAsync(
                emailHash, ipAddress ?? "", PasswordResetRequestOutcomeEnum.UserNotFound, ct);
            return Result.Success(); // Never reveal whether the email is registered
        }

        if (!user.HasPassword())
        {
            await passwordResetRequestRepository.LogAsync(
                emailHash, ipAddress ?? "", PasswordResetRequestOutcomeEnum.OAuthOnly, ct);
            return Result.Success(); // Never reveal that the account is OAuth-only
        }

        var internalId = await userRepository.GetInternalIdByPublicIdAsync(user.PublicId.Value, ct);
        if (internalId is null)
            return Result.Success();

        await passwordResetTokenRepository.InvalidateAllForUserAsync(internalId.Value, ct);

        var rawTokenBytes = new byte[32];
        RandomNumberGenerator.Fill(rawTokenBytes);
        var rawToken = Convert.ToBase64String(rawTokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        await passwordResetTokenRepository.CreateAsync(
            internalId.Value, tokenHash, DateTime.UtcNow.AddHours(1), ipAddress, userAgent, ct);

        await passwordResetRequestRepository.LogAsync(
            emailHash, ipAddress ?? "", PasswordResetRequestOutcomeEnum.UserFound, ct);

        await emailSender.SendPasswordResetAsync(
            user.Email!, user.DisplayName ?? "there", rawToken, baseUrl, ct);

        return Result.Success();
    }

    public async Task<Result> CompletePasswordResetAsync(
        string rawToken, string newPassword, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return Result.Failure("Reset token is required.");

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        var tokenDto = await passwordResetTokenRepository.GetByTokenHashAsync(tokenHash, ct);

        if (tokenDto is null || tokenDto.UsedAt.HasValue || tokenDto.ExpiresAt <= DateTime.UtcNow)
            return Result.Failure("This password reset link is invalid or has expired.");

        if (string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure("New password is required.");
        if (newPassword.Length < 8)
            return Result.Failure("Password must be at least 8 characters.");
        if (!Regex.IsMatch(newPassword, @"[A-Z]"))
            return Result.Failure("Password must contain at least one uppercase letter.");
        if (!Regex.IsMatch(newPassword, @"[a-z]"))
            return Result.Failure("Password must contain at least one lowercase letter.");
        if (!Regex.IsMatch(newPassword, @"\d"))
            return Result.Failure("Password must contain at least one number.");
        if (!Regex.IsMatch(newPassword, @"[^a-zA-Z0-9]"))
            return Result.Failure("Password must contain at least one special character.");

        var userId = UserId.From(tokenDto.UserPublicId);
        var user = await userRepository.GetByPublicIdAsync(userId, ct);

        if (user is null)
            return Result.Failure("This password reset link is invalid or has expired.");

        user.SetPasswordHash(passwordHasher.HashPassword(newPassword));
        user.IncrementAuthVersion();
        await userRepository.UpdateAsync(user, ct);
        await authVersionCache.SetAsync(user.PublicId.Value, user.AuthVersion, ct);

        await refreshTokenRepository.RevokeAllForUserAsync(userId);

        await passwordResetTokenRepository.MarkUsedAsync(tokenDto.Id, ipAddress, userAgent, ct);
        await passwordResetTokenRepository.InvalidateAllForUserAsync(tokenDto.UserId, ct);

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(UserId userId, string? currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await userRepository.GetByPublicIdAsync(userId, ct);
        if (user is null)
            return Result.Failure("User not found.");

        if (currentPassword is not null)
        {
            if (!user.HasPassword())
                return Result.Failure("No password is set on this account.");

            if (!passwordHasher.VerifyPassword(currentPassword, user.PasswordHash!))
                return Result.Failure("Current password is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure("New password is required.");
        if (newPassword.Length < 8)
            return Result.Failure("Password must be at least 8 characters.");
        if (!Regex.IsMatch(newPassword, @"[A-Z]"))
            return Result.Failure("Password must contain at least one uppercase letter.");
        if (!Regex.IsMatch(newPassword, @"[a-z]"))
            return Result.Failure("Password must contain at least one lowercase letter.");
        if (!Regex.IsMatch(newPassword, @"\d"))
            return Result.Failure("Password must contain at least one number.");
        if (!Regex.IsMatch(newPassword, @"[^a-zA-Z0-9]"))
            return Result.Failure("Password must contain at least one special character.");

        user.SetPasswordHash(passwordHasher.HashPassword(newPassword));
        user.IncrementAuthVersion();
        await userRepository.UpdateAsync(user, ct);
        await authVersionCache.SetAsync(user.PublicId.Value, user.AuthVersion, ct);
        return Result.Success();
    }
}
