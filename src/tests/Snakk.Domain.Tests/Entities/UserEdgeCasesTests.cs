using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class UserEdgeCasesTests
{
    #region Anonymize Edge Cases

    [Test]
    public async Task Anonymize_ClearsPII_DisplayNameBecomesAnonymousUser()
    {
        // Arrange
        var user = User.CreateWithEmail("john.doe", "john@example.com", "hash123", "token");

        // Act
        user.Anonymize();

        // Assert
        await Assert.That(user.DisplayName).IsEqualTo("Anonymous User");
        await Assert.That(user.Email).IsNull();
        await Assert.That(user.OAuthProviderId).IsNull();
    }

    [Test]
    public async Task Anonymize_OAuthUser_ClearsOAuthProviderId()
    {
        // Arrange
        var user = User.CreateWithOAuth("Jane Smith", "jane@example.com", "Google", "google-id-12345");
        await Assert.That(user.OAuthProviderId).IsEqualTo("google-id-12345");

        // Act
        user.Anonymize();

        // Assert
        await Assert.That(user.OAuthProviderId).IsNull();
        await Assert.That(user.DisplayName).IsEqualTo("Anonymous User");
        await Assert.That(user.Email).IsNull();
    }

    [Test]
    public async Task Anonymize_CalledTwice_IsIdempotent()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        user.Anonymize();
        user.Anonymize();

        // Assert
        await Assert.That(user.DisplayName).IsEqualTo("Anonymous User");
        await Assert.That(user.Email).IsNull();
    }

    [Test]
    public async Task Anonymize_PreservesPasswordHash()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash123", "token");

        // Act
        user.Anonymize();

        // Assert - password hash should remain intact (important for account state)
        await Assert.That(user.PasswordHash).IsEqualTo("hash123");
    }

    #endregion

    #region Avatar Management

    [Test]
    public async Task SetAvatarFileName_WithFileName_SetsFileName()
    {
        // Arrange
        var user = User.Create("testuser");

        // Act
        user.SetAvatarFileName("my-avatar.png");

        // Assert
        await Assert.That(user.AvatarFileName).IsEqualTo("my-avatar.png");
    }

    [Test]
    public async Task SetAvatarFileName_WithNull_ClearsFileName()
    {
        // Arrange
        var user = User.Create("testuser");
        user.SetAvatarFileName("avatar.png");
        await Assert.That(user.AvatarFileName).IsNotNull();

        // Act
        user.SetAvatarFileName(null);

        // Assert
        await Assert.That(user.AvatarFileName).IsNull();
    }

    [Test]
    public async Task ClearAvatar_RemovesAvatarFileName()
    {
        // Arrange
        var user = User.Create("testuser");
        user.SetAvatarFileName("avatar123.jpg");

        // Act
        user.ClearAvatar();

        // Assert
        await Assert.That(user.AvatarFileName).IsNull();
    }

    [Test]
    public async Task ClearAvatar_WhenNoAvatar_DoesNotThrow()
    {
        // Arrange
        var user = User.Create("testuser");
        await Assert.That(user.AvatarFileName).IsNull();

        // Act & Assert
        await Assert.That(() => user.ClearAvatar()).ThrowsNothing();
        await Assert.That(user.AvatarFileName).IsNull();
    }

    [Test]
    public async Task SetAvatarFileName_UpdatesLastModifiedAt()
    {
        // Arrange
        var user = User.Create("testuser");
        var originalModified = user.LastModifiedAt;

        // Act
        user.SetAvatarFileName("new-avatar.jpg");

        // Assert
        await Assert.That(user.LastModifiedAt).IsNotEqualTo(originalModified);
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    #endregion

    #region AvatarRevision Management

    [Test]
    public async Task NewUser_HasAvatarRevisionZero()
    {
        // Arrange & Act
        var user = User.Create("testuser");

        // Assert
        await Assert.That(user.AvatarRevision).IsEqualTo(0);
    }

    [Test]
    public async Task Rehydrate_WithAvatarRevision_PreservesRevision()
    {
        // Arrange & Act
        var user = User.Rehydrate(
            UserId.New(), "testuser", "test@example.com", "hash",
            true, null, null, null, null, "avatar.jpg",
            avatarRevision: 5,
            autoFollowOnReply: true,
            DateTime.UtcNow);

        // Assert
        await Assert.That(user.AvatarRevision).IsEqualTo(5);
    }

    #endregion

    #region AutoFollowOnReply Defaults

    [Test]
    public async Task NewUser_AutoFollowOnReply_DefaultsToTrue()
    {
        // Arrange & Act
        var user = User.Create("testuser");

        // Assert
        await Assert.That(user.AutoFollowOnReply).IsTrue();
    }

    [Test]
    public async Task SetAutoFollowOnReply_ChangesValue()
    {
        // Arrange
        var user = User.Create("testuser");
        await Assert.That(user.AutoFollowOnReply).IsTrue();

        // Act
        user.SetAutoFollowOnReply(false);

        // Assert
        await Assert.That(user.AutoFollowOnReply).IsFalse();
    }

    [Test]
    public async Task SetAutoFollowOnReply_UpdatesLastModifiedAt()
    {
        // Arrange
        var user = User.Create("testuser");

        // Act
        user.SetAutoFollowOnReply(false);

        // Assert
        await Assert.That(user.LastModifiedAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task CreateWithEmail_SetsDefaultPreferences()
    {
        // Arrange & Act
        var user = User.CreateWithEmail("user", "test@example.com", "hash", "token");

        // Assert
        await Assert.That(user.AutoFollowOnReply).IsTrue();
    }

    [Test]
    public async Task CreateWithOAuth_SetsDefaultPreferences()
    {
        // Arrange & Act
        var user = User.CreateWithOAuth("user", "test@example.com", "Google", "google-id");

        // Assert
        await Assert.That(user.AutoFollowOnReply).IsTrue();
    }

    #endregion

    #region Domain Events

    [Test]
    public async Task CreateWithEmail_AddsDomainEvent()
    {
        // Arrange & Act
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Assert
        await Assert.That(user.DomainEvents).Count().IsEqualTo(1);
        var createdEvent = user.DomainEvents.First() as UserCreatedEvent;
        await Assert.That(createdEvent).IsNotNull();
        await Assert.That(createdEvent!.UserId).IsEqualTo(user.PublicId);
    }

    [Test]
    public async Task MultipleDomainEvents_CanBeAdded()
    {
        // Arrange
        var user = User.Create("testuser");
        // Create has no domain event but CreateWithEmail does; let's use AddDomainEvent directly
        user.AddDomainEvent(new UserCreatedEvent(user.PublicId));
        user.AddDomainEvent(new UserDeletedEvent(user.PublicId));

        // Assert
        await Assert.That(user.DomainEvents).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ClearDomainEvents_ClearsAllEvents()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");
        await Assert.That(user.DomainEvents).Count().IsEqualTo(1);

        // Act
        user.ClearDomainEvents();

        // Assert
        await Assert.That(user.DomainEvents).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ClearDomainEvents_ThenAddNew_WorksCorrectly()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");
        user.ClearDomainEvents();
        await Assert.That(user.DomainEvents).IsEmpty();

        // Act
        user.AddDomainEvent(new UserCreatedEvent(user.PublicId));

        // Assert
        await Assert.That(user.DomainEvents).Count().IsEqualTo(1);
    }

    #endregion

    #region GenerateEmailVerificationToken

    [Test]
    public async Task GenerateEmailVerificationToken_GeneratesNonEmptyToken()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "old-token");

        // Act
        user.GenerateEmailVerificationToken();

        // Assert
        await Assert.That(user.EmailVerificationToken).IsNotNull();
        await Assert.That(user.EmailVerificationToken!.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GenerateEmailVerificationToken_TokenIs32Characters()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        user.GenerateEmailVerificationToken();

        // Assert - GUID without hyphens = 32 characters
        await Assert.That(user.EmailVerificationToken!).Length().IsEqualTo(32);
    }

    #endregion

    #region VerifyEmail

    [Test]
    public async Task VerifyEmail_ClearsTokenAndSetsVerified()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "verification-token");
        await Assert.That(user.EmailVerified).IsFalse();
        await Assert.That(user.EmailVerificationToken).IsEqualTo("verification-token");

        // Act
        user.VerifyEmail();

        // Assert
        await Assert.That(user.EmailVerified).IsTrue();
        await Assert.That(user.EmailVerificationToken).IsNull();
    }

    [Test]
    public async Task VerifyEmail_CalledTwice_IsIdempotent()
    {
        // Arrange
        var user = User.CreateWithEmail("testuser", "test@example.com", "hash", "token");

        // Act
        user.VerifyEmail();
        user.VerifyEmail();

        // Assert
        await Assert.That(user.EmailVerified).IsTrue();
        await Assert.That(user.EmailVerificationToken).IsNull();
    }

    #endregion

    #region HasPassword and IsOAuthUser

    [Test]
    public async Task HasPassword_ForOAuthUser_ReturnsFalse()
    {
        // Arrange
        var user = User.CreateWithOAuth("oauthuser", "oauth@example.com", "Google", "google-id");

        // Act & Assert
        await Assert.That(user.HasPassword()).IsFalse();
    }

    [Test]
    public async Task HasPassword_ForEmailUser_ReturnsTrue()
    {
        // Arrange
        var user = User.CreateWithEmail("emailuser", "email@example.com", "hash123", "token");

        // Act & Assert
        await Assert.That(user.HasPassword()).IsTrue();
    }

    [Test]
    public async Task IsOAuthUser_WhenOAuthProviderSet_ReturnsTrue()
    {
        // Arrange
        var user = User.CreateWithOAuth("oauthuser", "oauth@example.com", "Google", "google-id");

        // Act & Assert
        await Assert.That(user.IsOAuthUser()).IsTrue();
    }

    [Test]
    public async Task IsOAuthUser_WhenNoOAuthProvider_ReturnsFalse()
    {
        // Arrange
        var user = User.CreateWithEmail("emailuser", "email@example.com", "hash123", "token");

        // Act & Assert
        await Assert.That(user.IsOAuthUser()).IsFalse();
    }

    [Test]
    public async Task OAuthUser_ThenSetPassword_BothAreTrueForHybridUser()
    {
        // Arrange
        var user = User.CreateWithOAuth("hybriduser", "hybrid@example.com", "Google", "google-id");
        await Assert.That(user.HasPassword()).IsFalse();

        // Act
        user.SetPasswordHash("new-password-hash");

        // Assert
        await Assert.That(user.HasPassword()).IsTrue();
        await Assert.That(user.IsOAuthUser()).IsTrue();
    }

    #endregion

    #region UpdateLastSeen and UpdateLastLogin

    [Test]
    public async Task UpdateLastSeen_SetsLastSeenAt()
    {
        // Arrange
        var user = User.Create("testuser");
        var before = DateTime.UtcNow;

        // Act
        user.UpdateLastSeen();

        // Assert
        await Assert.That(user.LastSeenAt).IsNotNull();
        await Assert.That(user.LastSeenAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateLastLogin_SetsBothLastLoginAtAndLastSeenAt()
    {
        // Arrange
        var user = User.Create("testuser");

        // Act
        user.UpdateLastLogin();

        // Assert
        await Assert.That(user.LastLoginAt).IsNotNull();
        await Assert.That(user.LastSeenAt).IsNotNull();
        await Assert.That(user.LastLoginAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(user.LastSeenAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateLastSeen_DoesNotAffectLastLoginAt()
    {
        // Arrange
        var user = User.Create("testuser");
        user.UpdateLastLogin();
        var loginTime = user.LastLoginAt!.Value;
        Thread.Sleep(10);

        // Act
        user.UpdateLastSeen();

        // Assert
        await Assert.That(user.LastLoginAt!.Value).IsEqualTo(loginTime);
        await Assert.That(user.LastSeenAt!.Value).IsGreaterThan(loginTime);
    }

    #endregion

    #region Rehydrate With All Nullable Parameters

    [Test]
    public async Task Rehydrate_WithAllNullableParametersNull_CreatesValidUser()
    {
        // Arrange & Act
        var user = User.Rehydrate(
            UserId.New(),
            "testuser",
            email: null,
            passwordHash: null,
            emailVerified: false,
            emailVerificationToken: null,
            oauthProvider: null,
            oauthProviderId: null,
            role: null,
            avatarFileName: null,
            avatarRevision: 0,
            autoFollowOnReply: true,
            DateTime.UtcNow,
            lastModifiedAt: null,
            lastSeenAt: null,
            lastLoginAt: null);

        // Assert
        await Assert.That(user.Email).IsNull();
        await Assert.That(user.PasswordHash).IsNull();
        await Assert.That(user.EmailVerificationToken).IsNull();
        await Assert.That(user.OAuthProvider).IsNull();
        await Assert.That(user.OAuthProviderId).IsNull();
        await Assert.That(user.Role).IsNull();
        await Assert.That(user.AvatarFileName).IsNull();
        await Assert.That(user.LastModifiedAt).IsNull();
        await Assert.That(user.LastSeenAt).IsNull();
        await Assert.That(user.LastLoginAt).IsNull();
    }

    [Test]
    public async Task Rehydrate_WithAllNullableParametersPopulated_CreatesFullUser()
    {
        // Arrange
        var userId = UserId.New();
        var now = DateTime.UtcNow;

        // Act
        var user = User.Rehydrate(
            userId,
            "admin-user",
            email: "admin@example.com",
            passwordHash: "hashed-pass",
            emailVerified: true,
            emailVerificationToken: "some-token",
            oauthProvider: "GitHub",
            oauthProviderId: "github-123",
            role: "admin",
            avatarFileName: "admin-avatar.png",
            avatarRevision: 3,
            autoFollowOnReply: false,
            now.AddDays(-30),
            lastModifiedAt: now.AddDays(-1),
            lastSeenAt: now.AddHours(-1),
            lastLoginAt: now.AddHours(-2));

        // Assert
        await Assert.That(user.PublicId).IsEqualTo(userId);
        await Assert.That(user.DisplayName).IsEqualTo("admin-user");
        await Assert.That(user.Email).IsEqualTo("admin@example.com");
        await Assert.That(user.PasswordHash).IsEqualTo("hashed-pass");
        await Assert.That(user.EmailVerified).IsTrue();
        await Assert.That(user.EmailVerificationToken).IsEqualTo("some-token");
        await Assert.That(user.OAuthProvider).IsEqualTo("GitHub");
        await Assert.That(user.OAuthProviderId).IsEqualTo("github-123");
        await Assert.That(user.Role).IsEqualTo("admin");
        await Assert.That(user.AvatarFileName).IsEqualTo("admin-avatar.png");
        await Assert.That(user.AvatarRevision).IsEqualTo(3);
        await Assert.That(user.AutoFollowOnReply).IsFalse();
        await Assert.That(user.LastModifiedAt).IsNotNull();
        await Assert.That(user.LastSeenAt).IsNotNull();
        await Assert.That(user.LastLoginAt).IsNotNull();
    }

    [Test]
    public async Task Rehydrate_DoesNotGenerateDomainEvents()
    {
        // Arrange & Act
        var user = User.Rehydrate(
            UserId.New(), "testuser", null, null, false, null, null, null, null, null,
            0, true, DateTime.UtcNow);

        // Assert
        await Assert.That(user.DomainEvents).IsEmpty();
    }

    #endregion
}
