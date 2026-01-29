namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class UserMapper
{
    public static User FromPersistence(this UserDatabaseEntity entity)
    {
        return User.Rehydrate(
            UserId.From(entity.PublicId),
            entity.DisplayName,
            entity.Email,
            entity.PasswordHash,
            entity.EmailVerified,
            entity.EmailVerificationToken,
            entity.OAuthProvider,
            entity.OAuthProviderId,
            entity.Role,
            entity.AvatarFileName,
            entity.PreferEndlessScroll,
            entity.CreatedAt,
            entity.LastModifiedAt,
            entity.LastSeenAt,
            entity.LastLoginAt);
    }

    public static UserDatabaseEntity ToPersistence(this User user)
    {
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
            Role = user.Role,
            AvatarFileName = user.AvatarFileName,
            PreferEndlessScroll = user.PreferEndlessScroll,
            CreatedAt = user.CreatedAt,
            LastModifiedAt = user.LastModifiedAt,
            LastLoginAt = user.LastLoginAt,
            LastSeenAt = user.LastSeenAt
        };
    }
}
