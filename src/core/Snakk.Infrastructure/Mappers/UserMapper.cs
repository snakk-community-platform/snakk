namespace Snakk.Infrastructure.Mappers;

using System.Linq;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class UserMapper
{
    public static User FromPersistence(this UserDatabaseEntity entity)
    {
        // Check if user has GlobalAdmin role in the Roles collection
        // Note: Roles collection must be loaded for this to work
        string? role = entity.Roles?.Any(r => r.RoleId == (int)UserRoleTypeEnum.GlobalAdmin && r.RevokedAt == null) == true
            ? "Admin"
            : null;

        return User.Rehydrate(
            UserId.From(entity.PublicId),
            entity.DisplayName,
            entity.Email,
            entity.PasswordHash,
            entity.EmailVerified,
            entity.EmailVerificationToken,
            entity.OAuthProvider,
            entity.OAuthProviderId,
            role,
            entity.AvatarFileName,
            entity.AvatarRevision,
            entity.PreferEndlessScroll,
            entity.AutoFollowOnReply,
            entity.CreatedAt,
            entity.LastModifiedAt,
            entity.LastSeenAt,
            entity.LastLoginAt,
            entity.NeedsProfileSetup);
    }

    public static UserDatabaseEntity ToPersistence(this User user)
    {
        // Note: User.Role is derived from UserRoles collection, not stored as a column
        // Role assignments are managed through the UserRole table
        return new UserDatabaseEntity
        {
            PublicId = user.PublicId,
            DisplayName = user.DisplayName,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            EmailVerified = user.EmailVerified,
            EmailVerificationToken = user.EmailVerificationToken,
            OAuthProvider = user.OAuthProvider,
            OAuthProviderId = user.OAuthProviderId,
            AvatarFileName = user.AvatarFileName,
            AvatarRevision = user.AvatarRevision,
            PreferEndlessScroll = user.PreferEndlessScroll,
            AutoFollowOnReply = user.AutoFollowOnReply,
            NeedsProfileSetup = user.NeedsProfileSetup,
            CreatedAt = user.CreatedAt,
            LastModifiedAt = user.LastModifiedAt,
            LastLoginAt = user.LastLoginAt,
            LastSeenAt = user.LastSeenAt
        };
    }
}
