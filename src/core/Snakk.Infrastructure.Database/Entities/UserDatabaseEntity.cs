namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("User")]
public class UserDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Required attributes
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? EmailHash { get; set; } // SHA-256 hash of lowercased email for lookups
    public string? PasswordHash { get; set; }
    public bool EmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }
    public string? OAuthProvider { get; set; }
    public string? OAuthProviderId { get; set; }
    public required DateTime CreatedAt { get; set; }

    // Other attributes
    public DateTime? LastModifiedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }

    // Avatar: uploaded filename (null = use generated avatar)
    public string? AvatarFileName { get; set; }
    public string? AvatarThumbnailFileName { get; set; }
    public string? AvatarMicroFileName { get; set; }

    // Avatar revision number (incremented when user changes avatar)
    public int AvatarRevision { get; set; } = 0;

    // User preferences
    public bool AutoFollowOnReply { get; set; } = true;
    public string? Timezone { get; set; }
    public string? Bio { get; set; }
    public string? FeedToken { get; set; }
    public bool? AllowAdultContent { get; set; }
    public int AdultPreviewImageMode { get; set; } // 0=Show, 1=Blur, 2=Hide
    public bool HidePresence { get; set; } = false;

    // Denormalized counters (maintained by CounterService)
    public int DiscussionCount { get; set; }
    public int ReplyCount { get; set; }
    public int FollowerCount { get; set; }
    public int UnreadNotificationCount { get; set; }

    // Display name change tracking
    public DateTime? DisplayNameChangedAt { get; set; }
    public bool IsDisplayNameLocked { get; set; } = false;

    // Profile setup flag (true for new OAuth users until they choose a display name)
    public bool NeedsProfileSetup { get; set; } = false;

    // Account lockout (brute-force protection)
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }

    // 2FA (Two-Factor Authentication)
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; } // Base32-encoded TOTP secret
    public DateTime? TwoFactorEnabledAt { get; set; }

    // Discord account linking (separate from OAuthProvider/OAuthProviderId which controls login)
    public string? DiscordUserId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? DiscordAvatarHash { get; set; }
    public string? DiscordLinkToken { get; set; }         // Single-use GUID, 15-min TTL
    public DateTime? DiscordLinkTokenExpiry { get; set; } // UTC

    // Navigation properties
    public virtual ICollection<TwoFactorTrustedDeviceDatabaseEntity> TwoFactorTrustedDevices { get; set; } = [];
    public virtual ICollection<TwoFactorBackupCodeDatabaseEntity> TwoFactorBackupCodes { get; set; } = [];
    public virtual ICollection<RefreshTokenDatabaseEntity> RefreshTokens { get; set; } = [];
    public virtual ICollection<UserRoleDatabaseEntity> Roles { get; set; } = [];
    public virtual ICollection<UserSocialLinkDatabaseEntity> SocialLinks { get; set; } = [];
}
