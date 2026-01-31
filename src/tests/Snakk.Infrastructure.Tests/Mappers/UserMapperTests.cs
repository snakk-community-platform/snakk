using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class UserMapperTests
{
    #region ToPersistence Tests

    [Fact]
    public void ToPersistence_WithEmailUser_MapsAllProperties()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "password_hash", "verification_token");

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.Should().NotBeNull();
        entity.PublicId.Should().Be(user.PublicId);
        entity.DisplayName.Should().Be("TestUser");
        entity.Email.Should().Be("test@example.com");
        entity.PasswordHash.Should().Be("password_hash");
        entity.EmailVerified.Should().BeFalse();
        entity.EmailVerificationToken.Should().Be("verification_token");
        entity.OAuthProvider.Should().BeNull();
        entity.OAuthProviderId.Should().BeNull();
        entity.RoleId.Should().BeNull();
        entity.AvatarFileName.Should().BeNull();
        // Note: PreferEndlessScroll defaults to true in the User entity
        entity.PreferEndlessScroll.Should().BeTrue();
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ToPersistence_WithOAuthUser_MapsOAuthProperties()
    {
        // Arrange
        var user = User.CreateWithOAuth("OAuthUser", "oauth@example.com", "google", "google_123");

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.PublicId.Should().Be(user.PublicId);
        entity.DisplayName.Should().Be("OAuthUser");
        entity.Email.Should().Be("oauth@example.com");
        entity.PasswordHash.Should().BeNull();
        entity.EmailVerified.Should().BeTrue();
        entity.EmailVerificationToken.Should().BeNull();
        entity.OAuthProvider.Should().Be("google");
        entity.OAuthProviderId.Should().Be("google_123");
    }

    [Fact]
    public void ToPersistence_WithAdminRole_MapsToAdminRoleId()
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
            false,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.RoleId.Should().Be((int)UserRoleEnum.Admin);
    }

    [Fact]
    public void ToPersistence_WithModRole_MapsToModRoleId()
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
            false,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.RoleId.Should().Be((int)UserRoleEnum.Mod);
    }

    [Fact]
    public void ToPersistence_WithAdminRoleCaseInsensitive_MapsCorrectly()
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
            false,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.RoleId.Should().Be((int)UserRoleEnum.Admin);
    }

    [Fact]
    public void ToPersistence_WithNullRole_MapsToNullRoleId()
    {
        // Arrange
        var user = User.CreateWithEmail("RegularUser", "user@example.com", "hash", "token");

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.RoleId.Should().BeNull();
    }

    [Fact]
    public void ToPersistence_WithInvalidRole_MapsToNullRoleId()
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
            false,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.RoleId.Should().BeNull();
    }

    [Fact]
    public void ToPersistence_WithAvatarFileName_MapsAvatarFileName()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        user.SetAvatarFileName("avatar.jpg");

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.AvatarFileName.Should().Be("avatar.jpg");
    }

    [Fact]
    public void ToPersistence_WithPreferEndlessScroll_MapsPreference()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        user.SetPreferEndlessScroll(true);

        // Act
        var entity = user.ToPersistence();

        // Assert
        entity.PreferEndlessScroll.Should().BeTrue();
    }

    #endregion

    #region FromPersistence Tests

    [Fact]
    public void FromPersistence_WithEmailUserEntity_ReconstructsUser()
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
            RoleId = null,
            AvatarFileName = null,
            PreferEndlessScroll = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        user.Should().NotBeNull();
        user.PublicId.Value.Should().Be(entity.PublicId);
        user.DisplayName.Should().Be("TestUser");
        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hash");
        user.EmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().Be("token");
        user.OAuthProvider.Should().BeNull();
        user.OAuthProviderId.Should().BeNull();
        user.Role.Should().BeNull();
        user.AvatarFileName.Should().BeNull();
        user.PreferEndlessScroll.Should().BeFalse();
    }

    [Fact]
    public void FromPersistence_WithOAuthUserEntity_ReconstructsOAuthUser()
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
            RoleId = null,
            AvatarFileName = null,
            PreferEndlessScroll = false,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        user.OAuthProvider.Should().Be("google");
        user.OAuthProviderId.Should().Be("google_123");
        user.PasswordHash.Should().BeNull();
        user.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public void FromPersistence_WithAdminRoleId_MapsToAdminRole()
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
            RoleId = (int)UserRoleEnum.Admin,
            AvatarFileName = null,
            PreferEndlessScroll = false,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        user.Role.Should().Be("Admin");
    }

    [Fact]
    public void FromPersistence_WithModRoleId_MapsToModRole()
    {
        // Arrange
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
            RoleId = (int)UserRoleEnum.Mod,
            AvatarFileName = null,
            PreferEndlessScroll = false,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        user.Role.Should().Be("Mod");
    }

    [Fact]
    public void FromPersistence_WithNullRoleId_MapsToNullRole()
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
            RoleId = null,
            AvatarFileName = null,
            PreferEndlessScroll = false,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        user.Role.Should().BeNull();
    }

    [Fact]
    public void FromPersistence_WithAvatarFileName_MapsAvatarFileName()
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
            RoleId = null,
            AvatarFileName = "avatar.png",
            PreferEndlessScroll = false,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        user.AvatarFileName.Should().Be("avatar.png");
    }

    [Fact]
    public void FromPersistence_WithPreferEndlessScroll_MapsPreference()
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
            RoleId = null,
            AvatarFileName = null,
            PreferEndlessScroll = true,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            LastSeenAt = null,
            LastLoginAt = null
        };

        // Act
        var user = entity.FromPersistence();

        // Assert
        user.PreferEndlessScroll.Should().BeTrue();
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_WithEmailUser_PreservesAllData()
    {
        // Arrange
        var originalUser = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        // Act
        var entity = originalUser.ToPersistence();
        var reconstructedUser = entity.FromPersistence();

        // Assert
        reconstructedUser.PublicId.Should().Be(originalUser.PublicId);
        reconstructedUser.DisplayName.Should().Be(originalUser.DisplayName);
        reconstructedUser.Email.Should().Be(originalUser.Email);
        reconstructedUser.PasswordHash.Should().Be(originalUser.PasswordHash);
        reconstructedUser.EmailVerified.Should().Be(originalUser.EmailVerified);
        reconstructedUser.EmailVerificationToken.Should().Be(originalUser.EmailVerificationToken);
        reconstructedUser.OAuthProvider.Should().Be(originalUser.OAuthProvider);
        reconstructedUser.OAuthProviderId.Should().Be(originalUser.OAuthProviderId);
        reconstructedUser.Role.Should().Be(originalUser.Role);
        reconstructedUser.AvatarFileName.Should().Be(originalUser.AvatarFileName);
        reconstructedUser.PreferEndlessScroll.Should().Be(originalUser.PreferEndlessScroll);
    }

    [Fact]
    public void RoundTrip_WithOAuthUser_PreservesAllData()
    {
        // Arrange
        var originalUser = User.CreateWithOAuth("OAuthUser", "oauth@example.com", "github", "github_456");

        // Act
        var entity = originalUser.ToPersistence();
        var reconstructedUser = entity.FromPersistence();

        // Assert
        reconstructedUser.PublicId.Should().Be(originalUser.PublicId);
        reconstructedUser.DisplayName.Should().Be(originalUser.DisplayName);
        reconstructedUser.Email.Should().Be(originalUser.Email);
        reconstructedUser.OAuthProvider.Should().Be(originalUser.OAuthProvider);
        reconstructedUser.OAuthProviderId.Should().Be(originalUser.OAuthProviderId);
        reconstructedUser.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_WithAdminRole_PreservesRole()
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
            false,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = originalUser.ToPersistence();
        var reconstructedUser = entity.FromPersistence();

        // Assert
        reconstructedUser.Role.Should().Be("Admin");
    }

    [Fact]
    public void RoundTrip_WithModRole_PreservesRole()
    {
        // Arrange
        var originalUser = User.Rehydrate(
            UserId.New(),
            "ModUser",
            "mod@example.com",
            "hash",
            true,
            null,
            null,
            null,
            "Mod",
            null,
            false,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);

        // Act
        var entity = originalUser.ToPersistence();
        var reconstructedUser = entity.FromPersistence();

        // Assert
        reconstructedUser.Role.Should().Be("Mod");
    }

    #endregion
}
