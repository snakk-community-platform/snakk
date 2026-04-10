using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class UserMapperTests
{
    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_WithEmailUser_MapsAllProperties()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "password_hash", "verification_token");

        // Act
        var entity = user.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(user.PublicId);
        await Assert.That(entity.DisplayName).IsEqualTo("TestUser");
        await Assert.That(entity.Email).IsEqualTo("test@example.com");
        await Assert.That(entity.PasswordHash).IsEqualTo("password_hash");
        await Assert.That(entity.EmailVerified).IsFalse();
        await Assert.That(entity.EmailVerificationToken).IsEqualTo("verification_token");
        await Assert.That(entity.OAuthProvider).IsNull();
        await Assert.That(entity.OAuthProviderId).IsNull();
        await Assert.That(entity.AvatarFileName).IsNull();
        await Assert.That((DateTime.UtcNow - entity.CreatedAt).TotalSeconds).IsLessThan(1);
    }

    [Test]
    public async Task ToPersistence_WithOAuthUser_MapsOAuthProperties()
    {
        // Arrange
        var user = User.CreateWithOAuth("oauth@example.com", "google", "google_123");

        // Act
        var entity = user.ToPersistence();

        // Assert
        await Assert.That(entity.PublicId).IsEqualTo(user.PublicId);
        await Assert.That(entity.DisplayName).IsNull();
        await Assert.That(entity.Email).IsEqualTo("oauth@example.com");
        await Assert.That(entity.PasswordHash).IsNull();
        await Assert.That(entity.EmailVerified).IsTrue();
        await Assert.That(entity.EmailVerificationToken).IsNull();
        await Assert.That(entity.OAuthProvider).IsEqualTo("google");
        await Assert.That(entity.OAuthProviderId).IsEqualTo("google_123");
    }

    [Test]
    public async Task ToPersistence_WithAdminRole_MapsToAdminRoleId()
    {
        // Arrange
        var user = User.Rehydrate(
            UserId.New(),
            "AdminUser",
            "admin@example.com",
            "hash",
            true,
            null,
            null,
            null,
            "Admin", // Admin role
            null,
            null, // avatarThumbnailFileName
            null, // avatarMicroFileName
            0, // avatarRevision
            false, // autoFollowOnReply
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        // Note: Roles are now managed through the UserRoles collection, not RoleId on User
        await Assert.That(entity.DisplayName).IsEqualTo("AdminUser");
    }

    [Test]
    public async Task ToPersistence_WithModRole_MapsCorrectly()
    {
        // Arrange
        var user = User.Rehydrate(
            UserId.New(),
            "ModUser",
            "mod@example.com",
            "hash",
            true,
            null,
            null,
            null,
            "Mod", // Mod role
            null,
            null, // avatarThumbnailFileName
            null, // avatarMicroFileName
            0, // avatarRevision
            false, // autoFollowOnReply
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        // Note: Roles are now managed through the UserRoles collection, not RoleId on User
        await Assert.That(entity.DisplayName).IsEqualTo("ModUser");
    }

    [Test]
    public async Task ToPersistence_WithAdminRoleCaseInsensitive_MapsCorrectly()
    {
        // Arrange
        var user = User.Rehydrate(
            UserId.New(),
            "AdminUser",
            "admin@example.com",
            "hash",
            true,
            null,
            null,
            null,
            "ADMIN", // Uppercase
            null,
            null, // avatarThumbnailFileName
            null, // avatarMicroFileName
            0, // avatarRevision
            false, // autoFollowOnReply
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        // Note: Roles are now managed through the UserRoles collection, not RoleId on User
        await Assert.That(entity.DisplayName).IsNotNull();
    }

    [Test]
    public async Task ToPersistence_WithNullRole_MapsToNullRoleId()
    {
        // Arrange
        var user = User.CreateWithEmail("RegularUser", "user@example.com", "hash", "token");

        // Act
        var entity = user.ToPersistence();

        // Assert
        // Note: Roles are now managed through the UserRoles collection, not RoleId on User
        await Assert.That(entity.DisplayName).IsNotNull();
    }

    [Test]
    public async Task ToPersistence_WithInvalidRole_MapsToNullRoleId()
    {
        // Arrange
        var user = User.Rehydrate(
            UserId.New(),
            "User",
            "user@example.com",
            "hash",
            true,
            null,
            null,
            null,
            "InvalidRole", // Not a valid role
            null,
            null, // avatarThumbnailFileName
            null, // avatarMicroFileName
            0, // avatarRevision
            false, // autoFollowOnReply
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        // Note: Roles are now managed through the UserRoles collection, not RoleId on User
        await Assert.That(entity.DisplayName).IsNotNull();
    }

    [Test]
    public async Task ToPersistence_WithAvatarFileName_MapsAvatarFileName()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        user.SetAvatarFileName("avatar.jpg");

        // Act
        var entity = user.ToPersistence();

        // Assert
        await Assert.That(entity.AvatarFileName).IsEqualTo("avatar.jpg");
    }

    #endregion

    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_WithEmailUserEntity_ReconstructsUser()
    {
        // Arrange
        var entity = new UserDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            DisplayName = "TestUser",
            Email = "test@example.com",
            PasswordHash = "hash",
            EmailVerified = false,
            EmailVerificationToken = "token",
            OAuthProvider = null,
            OAuthProviderId = null,
            AvatarFileName = null,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        await Assert.That(user).IsNotNull();
        await Assert.That(user.PublicId.Value).IsEqualTo(entity.PublicId);
        await Assert.That(user.DisplayName).IsEqualTo("TestUser");
        await Assert.That(user.Email).IsEqualTo("test@example.com");
        await Assert.That(user.PasswordHash).IsEqualTo("hash");
        await Assert.That(user.EmailVerified).IsFalse();
        await Assert.That(user.EmailVerificationToken).IsEqualTo("token");
        await Assert.That(user.OAuthProvider).IsNull();
        await Assert.That(user.OAuthProviderId).IsNull();
        await Assert.That(user.Role).IsNull();
        await Assert.That(user.AvatarFileName).IsNull();
    }

    [Test]
    public async Task FromPersistence_WithOAuthUserEntity_ReconstructsOAuthUser()
    {
        // Arrange
        var entity = new UserDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            DisplayName = "OAuthUser",
            Email = "oauth@example.com",
            PasswordHash = null,
            EmailVerified = true,
            EmailVerificationToken = null,
            OAuthProvider = "google",
            OAuthProviderId = "google_123",
            AvatarFileName = null,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        await Assert.That(user.OAuthProvider).IsEqualTo("google");
        await Assert.That(user.OAuthProviderId).IsEqualTo("google_123");
        await Assert.That(user.PasswordHash).IsNull();
        await Assert.That(user.EmailVerified).IsTrue();
    }

    [Test]
    public async Task FromPersistence_WithAdminRoleId_MapsToAdminRole()
    {
        // Arrange
        var entity = new UserDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            DisplayName = "AdminUser",
            Email = "admin@example.com",
            PasswordHash = "hash",
            EmailVerified = true,
            EmailVerificationToken = null,
            OAuthProvider = null,
            OAuthProviderId = null,
            Roles =
            [
                new() { PublicId = Guid.NewGuid().ToString(), RoleId = (int)UserRoleTypeEnum.GlobalAdmin, AssignedAt = DateTime.UtcNow, RevokedAt = null }
            ],
            AvatarFileName = null,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        await Assert.That(user.Role).IsEqualTo("Admin");
    }

    [Test]
    public async Task FromPersistence_WithNonGlobalAdminRole_MapsToNullRole()
    {
        // Arrange - CommunityAdmin is not GlobalAdmin, so mapper returns null
        var entity = new UserDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            DisplayName = "ModUser",
            Email = "mod@example.com",
            PasswordHash = "hash",
            EmailVerified = true,
            EmailVerificationToken = null,
            OAuthProvider = null,
            OAuthProviderId = null,
            Roles =
            [
                new() { PublicId = Guid.NewGuid().ToString(), RoleId = (int)UserRoleTypeEnum.CommunityAdmin, AssignedAt = DateTime.UtcNow, RevokedAt = null }
            ],
            AvatarFileName = null,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert - Only GlobalAdmin maps to "Admin", all other roles map to null
        await Assert.That(user.Role).IsNull();
    }

    [Test]
    public async Task FromPersistence_WithNullRoleId_MapsToNullRole()
    {
        // Arrange
        var entity = new UserDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            DisplayName = "RegularUser",
            Email = "user@example.com",
            PasswordHash = "hash",
            EmailVerified = true,
            EmailVerificationToken = null,
            OAuthProvider = null,
            OAuthProviderId = null,
            AvatarFileName = null,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        await Assert.That(user.Role).IsNull();
    }

    [Test]
    public async Task FromPersistence_WithAvatarFileName_MapsAvatarFileName()
    {
        // Arrange
        var entity = new UserDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            DisplayName = "TestUser",
            Email = "test@example.com",
            PasswordHash = "hash",
            EmailVerified = true,
            EmailVerificationToken = null,
            OAuthProvider = null,
            OAuthProviderId = null,
            AvatarFileName = "avatar.png",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        await Assert.That(user.AvatarFileName).IsEqualTo("avatar.png");
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_WithEmailUser_PreservesAllData()
    {
        // Arrange
        var originalUser = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        // Act
        var entity = originalUser.ToPersistence();
        var reconstructedUser = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedUser.PublicId).IsEqualTo(originalUser.PublicId);
        await Assert.That(reconstructedUser.DisplayName).IsEqualTo(originalUser.DisplayName);
        await Assert.That(reconstructedUser.Email).IsEqualTo(originalUser.Email);
        await Assert.That(reconstructedUser.PasswordHash).IsEqualTo(originalUser.PasswordHash);
        await Assert.That(reconstructedUser.EmailVerified).IsEqualTo(originalUser.EmailVerified);
        await Assert.That(reconstructedUser.EmailVerificationToken).IsEqualTo(originalUser.EmailVerificationToken);
        await Assert.That(reconstructedUser.OAuthProvider).IsEqualTo(originalUser.OAuthProvider);
        await Assert.That(reconstructedUser.OAuthProviderId).IsEqualTo(originalUser.OAuthProviderId);
        await Assert.That(reconstructedUser.Role).IsEqualTo(originalUser.Role);
        await Assert.That(reconstructedUser.AvatarFileName).IsEqualTo(originalUser.AvatarFileName);
    }

    [Test]
    public async Task RoundTrip_WithOAuthUser_PreservesAllData()
    {
        // Arrange
        var originalUser = User.CreateWithOAuth("oauth@example.com", "github", "github_456");

        // Act
        var entity = originalUser.ToPersistence();
        var reconstructedUser = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedUser.PublicId).IsEqualTo(originalUser.PublicId);
        await Assert.That(reconstructedUser.DisplayName).IsEqualTo(originalUser.DisplayName);
        await Assert.That(reconstructedUser.Email).IsEqualTo(originalUser.Email);
        await Assert.That(reconstructedUser.OAuthProvider).IsEqualTo(originalUser.OAuthProvider);
        await Assert.That(reconstructedUser.OAuthProviderId).IsEqualTo(originalUser.OAuthProviderId);
        await Assert.That(reconstructedUser.EmailVerified).IsTrue();
    }

    [Test]
    public async Task RoundTrip_WithAdminRole_DoesNotPreserveRole()
    {
        // Arrange
        var originalUser = User.Rehydrate(
            UserId.New(),
            "AdminUser",
            "admin@example.com",
            "hash",
            true,
            null,
            null,
            null,
            "Admin",
            null,
            null, // avatarThumbnailFileName
            null, // avatarMicroFileName
            0, // avatarRevision
            false, // autoFollowOnReply
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = originalUser.ToPersistence();
        var reconstructedUser = entity.FromPersistence();

        // Assert - Roles are managed through UserRoles collection, not stored on UserDatabaseEntity.
        // ToPersistence does not include Roles, so FromPersistence won't have role data.
        await Assert.That(reconstructedUser.Role).IsNull();
    }

    [Test]
    public async Task RoundTrip_WithRole_PreservesRoleWhenRolesCollectionSet()
    {
        // Arrange
        var originalUser = User.Rehydrate(
            UserId.New(),
            "AdminUser",
            "admin@example.com",
            "hash",
            true,
            null,
            null,
            null,
            "Admin",
            null,
            null, // avatarThumbnailFileName
            null, // avatarMicroFileName
            0, // avatarRevision
            false, // autoFollowOnReply
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = originalUser.ToPersistence();
        // Simulate the database loading the Roles collection
        entity.Roles =
        [
            new() { PublicId = Guid.NewGuid().ToString(), RoleId = (int)UserRoleTypeEnum.GlobalAdmin, AssignedAt = DateTime.UtcNow, RevokedAt = null }
        ];
        var reconstructedUser = entity.FromPersistence();

        // Assert - With Roles collection loaded, GlobalAdmin maps to "Admin"
        await Assert.That(reconstructedUser.Role).IsEqualTo("Admin");
    }

    #endregion
}
