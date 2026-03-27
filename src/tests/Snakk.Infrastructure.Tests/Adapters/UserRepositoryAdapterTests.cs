using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Infrastructure.Tests.Adapters;

public class UserRepositoryAdapterTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly UserRepositoryAdapter _adapter;

    public UserRepositoryAdapterTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        var databaseRepo = new UserRepository(_db.Context);
        _adapter = new UserRepositoryAdapter(databaseRepo, _db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingUser_ReturnsMappedDomainEntity()
    {
        var dbUser = await _builder.CreateUserAsync("TestUser", "test@example.com");

        var result = await _adapter.GetByPublicIdAsync(UserId.From(dbUser.PublicId));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId.Value).IsEqualTo(dbUser.PublicId);
        await Assert.That(result.DisplayName).IsEqualTo("TestUser");
        await Assert.That(result.Email).IsEqualTo("test@example.com");
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentUser_ReturnsNull()
    {
        var result = await _adapter.GetByPublicIdAsync(UserId.From("nonexistent_id"));

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdsAsync Tests

    [Test]
    public async Task GetByPublicIdsAsync_MultipleUsers_ReturnsBatchOfDomainEntities()
    {
        var user1 = await _builder.CreateUserAsync("User One");
        var user2 = await _builder.CreateUserAsync("User Two");
        var user3 = await _builder.CreateUserAsync("User Three");

        var result = (await _adapter.GetByPublicIdsAsync(
            new[] { UserId.From(user1.PublicId), UserId.From(user2.PublicId), UserId.From(user3.PublicId) })).ToList();

        await Assert.That(result).Count().IsEqualTo(3);

        var displayNames = result
            .Select(u => u.DisplayName)
            .OrderBy(n => n)
            .ToList();
        await Assert.That(displayNames).Contains("User One");
        await Assert.That(displayNames).Contains("User Two");
        await Assert.That(displayNames).Contains("User Three");
    }

    #endregion

    #region GetByEmailAsync Tests

    [Test]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsMappedDomainEntity()
    {
        await _builder.CreateUserAsync("EmailUser", "findme@example.com");

        var result = await _adapter.GetByEmailAsync("findme@example.com");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Email).IsEqualTo("findme@example.com");
        await Assert.That(result.DisplayName).IsEqualTo("EmailUser");
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_ExistingUser_PatchesFields()
    {
        var dbUser = await _builder.CreateUserAsync("OriginalName", "original@example.com");

        // Get the domain user, modify it, then update
        var domainUser = (await _adapter.GetByPublicIdAsync(UserId.From(dbUser.PublicId)))!;
        domainUser.UpdateDisplayName("UpdatedName");
        domainUser.UpdateLastSeen();

        await _adapter.UpdateAsync(domainUser);

        // Verify changes persisted using a separate context
        var verifyContext = _db.CreateSeparateContext();
        var verifiedUser = await verifyContext.Users.FindAsync(dbUser.Id);

        await Assert.That(verifiedUser).IsNotNull();
        await Assert.That(verifiedUser!.DisplayName).IsEqualTo("UpdatedName");
        await Assert.That(verifiedUser.LastModifiedAt).IsNotNull();
        await Assert.That(verifiedUser.LastSeenAt).IsNotNull();
    }

    [Test]
    public async Task UpdateAsync_NonExistentPublicId_ThrowsInvalidOperationException()
    {
        var fakeUser = User.Rehydrate(
            UserId.From("nonexistent_id"),
            "FakeUser",
            "fake@example.com",
            passwordHash: null,
            emailVerified: false,
            emailVerificationToken: null,
            oauthProvider: null,
            oauthProviderId: null,
            role: null,
            avatarFileName: null,
            avatarRevision: 0,
            autoFollowOnReply: true,
            DateTime.UtcNow);

        await Assert.That(async () => await _adapter.UpdateAsync(fakeUser))
            .ThrowsExactly<InvalidOperationException>();
    }

    #endregion

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_NewUser_PersistsToDatabase()
    {
        var newUser = User.Create("NewUser", "newuser@example.com");

        await _adapter.AddAsync(newUser);

        // Verify persisted using a separate context
        var verifyContext = _db.CreateSeparateContext();
        var persisted = verifyContext.Users.FirstOrDefault(u => u.PublicId == newUser.PublicId.Value);

        await Assert.That(persisted).IsNotNull();
        await Assert.That(persisted!.DisplayName).IsEqualTo("NewUser");
        await Assert.That(persisted.Email).IsEqualTo("newuser@example.com");
    }

    #endregion
}
