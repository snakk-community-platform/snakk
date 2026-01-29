namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class User
{
    public UserId PublicId { get; private set; }
    public string DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public bool EmailVerified { get; private set; }
    public string? EmailVerificationToken { get; private set; }
    public string? OAuthProvider { get; private set; }
    public string? OAuthProviderId { get; private set; }
    public string? Role { get; private set; } // "admin", "mod", or null for regular users
    public string? AvatarFileName { get; private set; } // Uploaded avatar filename (null = use generated)
    public bool PreferEndlessScroll { get; private set; } = true; // User preference for endless scroll vs pagination
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private User()
    {
    }
#pragma warning restore CS8618

    private User(
        UserId publicId,
        string displayName,
        string? email,
        string? passwordHash,
        bool emailVerified,
        string? emailVerificationToken,
        string? oauthProvider,
        string? oauthProviderId,
        string? role,
        string? avatarFileName,
        bool preferEndlessScroll,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? lastSeenAt = null,
        DateTime? lastLoginAt = null)
    {
        PublicId = publicId;
        DisplayName = displayName;
        Email = email;
        PasswordHash = passwordHash;
        EmailVerified = emailVerified;
        EmailVerificationToken = emailVerificationToken;
        OAuthProvider = oauthProvider;
        OAuthProviderId = oauthProviderId;
        Role = role;
        AvatarFileName = avatarFileName;
        PreferEndlessScroll = preferEndlessScroll;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        LastSeenAt = lastSeenAt;
        LastLoginAt = lastLoginAt;
    }

    public static User CreateWithEmail(
        string displayName,
        string email,
        string passwordHash,
        string emailVerificationToken)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));

        return new User(
            UserId.New(),
            displayName,
            email,
            passwordHash,
            emailVerified: false,
            emailVerificationToken,
            oauthProvider: null,
            oauthProviderId: null,
            role: null,
            avatarFileName: null,
            preferEndlessScroll: true,
            DateTime.UtcNow,
            lastSeenAt: DateTime.UtcNow);
    }

    public static User CreateWithOAuth(
        string displayName,
        string email,
        string oauthProvider,
        string oauthProviderId)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        return new User(
            UserId.New(),
            displayName,
            email,
            passwordHash: null,
            emailVerified: true, // OAuth email is pre-verified
            emailVerificationToken: null,
            oauthProvider,
            oauthProviderId,
            role: null,
            avatarFileName: null,
            preferEndlessScroll: true,
            DateTime.UtcNow,
            lastSeenAt: DateTime.UtcNow);
    }

    public static User Create(
        string displayName,
        string? email = null,
        string? oauthProviderId = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));

        return new User(
            UserId.New(),
            displayName,
            email,
            passwordHash: null,
            emailVerified: false,
            emailVerificationToken: null,
            oauthProvider: null,
            oauthProviderId,
            role: null,
            avatarFileName: null,
            preferEndlessScroll: true,
            DateTime.UtcNow,
            lastSeenAt: DateTime.UtcNow);
    }

    public static User Rehydrate(
        UserId publicId,
        string displayName,
        string? email,
        string? passwordHash,
        bool emailVerified,
        string? emailVerificationToken,
        string? oauthProvider,
        string? oauthProviderId,
        string? role,
        string? avatarFileName,
        bool preferEndlessScroll,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? lastSeenAt = null,
        DateTime? lastLoginAt = null)
    {
        return new User(
            publicId,
            displayName,
            email,
            passwordHash,
            emailVerified,
            emailVerificationToken,
            oauthProvider,
            oauthProviderId,
            role,
            avatarFileName,
            preferEndlessScroll,
            createdAt,
            lastModifiedAt,
            lastSeenAt,
            lastLoginAt);
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));

        DisplayName = displayName;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateLastSeen()
    {
        LastSeenAt = DateTime.UtcNow;
    }

    public void Anonymize()
    {
        DisplayName = "Anonymous User";
        Email = null;
        OAuthProviderId = null;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));

        PasswordHash = passwordHash;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void VerifyEmail()
    {
        EmailVerified = true;
        EmailVerificationToken = null;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        LastSeenAt = DateTime.UtcNow;
    }

    public void GenerateEmailVerificationToken()
    {
        EmailVerificationToken = Guid.NewGuid().ToString("N");
        LastModifiedAt = DateTime.UtcNow;
    }

    public bool HasPassword()
    {
        return !string.IsNullOrEmpty(PasswordHash);
    }

    public bool IsOAuthUser()
    {
        return !string.IsNullOrEmpty(OAuthProvider);
    }

    public void SetAvatarFileName(string? fileName)
    {
        AvatarFileName = fileName;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void ClearAvatar()
    {
        AvatarFileName = null;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetPreferEndlessScroll(bool prefer)
    {
        PreferEndlessScroll = prefer;
        LastModifiedAt = DateTime.UtcNow;
    }
}
