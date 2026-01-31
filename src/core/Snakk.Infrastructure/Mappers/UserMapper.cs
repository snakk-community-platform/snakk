namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class UserMapper
{
    public static User FromPersistence(this UserDatabaseEntity entity)
    {
        // Convert int? RoleId to string? Role: 1="Admin", 2="Mod", null=null
        string? role = entity.RoleId.HasValue
            ? ((UserRoleEnum)entity.RoleId.Value) switch
            {
                UserRoleEnum.Admin => "Admin",
                UserRoleEnum.Mod => "Mod",
                _ => null
            }
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
            entity.PreferEndlessScroll,
            entity.CreatedAt,
            entity.LastModifiedAt,
            entity.LastSeenAt,
            entity.LastLoginAt);
    }

    public static UserDatabaseEntity ToPersistence(this User user)
    {
        // Convert string? Role to int? RoleId: "Admin"=1, "Mod"=2, null=null
        int? roleId = user.Role?.ToLowerInvariant() switch
        {
            "admin" => (int)UserRoleEnum.Admin,
            "mod" => (int)UserRoleEnum.Mod,
            _ => null
        };

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
            RoleId = roleId,
            AvatarFileName = user.AvatarFileName,
            PreferEndlessScroll = user.PreferEndlessScroll,
            CreatedAt = user.CreatedAt,
            LastModifiedAt = user.LastModifiedAt,
            LastLoginAt = user.LastLoginAt,
            LastSeenAt = user.LastSeenAt
        };
    }
}
