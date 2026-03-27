namespace Snakk.Domain.Entities;

using Snakk.Domain.Events;
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
    public int AvatarRevision { get; private set; } = 0; // Avatar revision number (incremented when avatar changes)
    public bool AutoFollowOnReply { get; private set; } = true; // Automatically follow discussions when replying
    public string? Timezone { get; private set; } // IANA timezone ID (null = use community/site setting)
    public string? Bio { get; private set; }
    public bool NeedsProfileSetup { get; private set; } // OAuth users need to choose a display name
    public DateTime? DisplayNameChangedAt { get; private set; }
    public bool IsDisplayNameLocked { get; private set; }
    public int DiscussionCount { get; private set; }
    public int ReplyCount { get; private set; }
    public int FollowerCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

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
        int avatarRevision,
        bool autoFollowOnReply,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? lastSeenAt = null,
        DateTime? lastLoginAt = null,
        bool needsProfileSetup = false,
        string? timezone = null,
        string? bio = null,
        int discussionCount = 0,
        int replyCount = 0,
        int followerCount = 0,
        DateTime? displayNameChangedAt = null,
        bool isDisplayNameLocked = false)
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
        AvatarRevision = avatarRevision;
        AutoFollowOnReply = autoFollowOnReply;
        Timezone = timezone;
        Bio = bio;
        NeedsProfileSetup = needsProfileSetup;
        DiscussionCount = discussionCount;
        ReplyCount = replyCount;
        FollowerCount = followerCount;
        DisplayNameChangedAt = displayNameChangedAt;
        IsDisplayNameLocked = isDisplayNameLocked;
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

        var user = new User(
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
            avatarRevision: 0,
            autoFollowOnReply: true,
            DateTime.UtcNow,
            lastSeenAt: DateTime.UtcNow);

        user.AddDomainEvent(new UserCreatedEvent(user.PublicId));

        return user;
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

        var user = new User(
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
            avatarRevision: 0,
            autoFollowOnReply: true,
            DateTime.UtcNow,
            lastSeenAt: DateTime.UtcNow,
            needsProfileSetup: true); // New OAuth users should choose their display name

        user.AddDomainEvent(new UserCreatedEvent(user.PublicId));

        return user;
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
            avatarRevision: 0,
            autoFollowOnReply: true,
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
        int avatarRevision,
        bool autoFollowOnReply,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        DateTime? lastSeenAt = null,
        DateTime? lastLoginAt = null,
        bool needsProfileSetup = false,
        string? timezone = null,
        string? bio = null,
        int discussionCount = 0,
        int replyCount = 0,
        int followerCount = 0,
        DateTime? displayNameChangedAt = null,
        bool isDisplayNameLocked = false) =>
        new User(
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
            avatarRevision,
            autoFollowOnReply,
            createdAt,
            lastModifiedAt,
            lastSeenAt,
            lastLoginAt,
            needsProfileSetup,
            timezone,
            bio,
            discussionCount,
            replyCount,
            followerCount,
            displayNameChangedAt,
            isDisplayNameLocked);

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));

        DisplayName = displayName;
        DisplayNameChangedAt = DateTime.UtcNow;
        NeedsProfileSetup = false;
        LastModifiedAt = DateTime.UtcNow;
    }

    public bool CanChangeDisplayName(int cooldownDays = 30)
    {
        if (IsDisplayNameLocked) return false;
        if (NeedsProfileSetup) return true; // First-time setup always allowed
        if (DisplayNameChangedAt is null) return true;
        return (DateTime.UtcNow - DisplayNameChangedAt.Value).TotalDays >= cooldownDays;
    }

    public int? GetCooldownDaysRemaining(int cooldownDays = 30)
    {
        if (DisplayNameChangedAt is null) return null;
        var elapsed = (DateTime.UtcNow - DisplayNameChangedAt.Value).TotalDays;
        return elapsed >= cooldownDays ? null : (int)Math.Ceiling(cooldownDays - elapsed);
    }

    public void LockDisplayName() => IsDisplayNameLocked = true;

    public void UnlockDisplayName() => IsDisplayNameLocked = false;

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

    public bool HasPassword() =>
        !string.IsNullOrEmpty(PasswordHash);

    public bool IsOAuthUser() =>
        !string.IsNullOrEmpty(OAuthProvider);

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

    public void SetAutoFollowOnReply(bool autoFollow)
    {
        AutoFollowOnReply = autoFollow;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetTimezone(string? timezone)
    {
        Timezone = timezone;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetBio(string? bio)
    {
        Bio = bio?.Length > 300 ? bio[..300] : bio;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
