namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("User")]
public class UserDatabaseEntity
{
    // Identifiers
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Required attributes
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
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

    // Role: "admin", "mod", or null for regular users
    public string? Role { get; set; }

    // Avatar: uploaded filename (null = use generated avatar)
    public string? AvatarFileName { get; set; }

    // User preferences
    public bool PreferEndlessScroll { get; set; } = true;
}
