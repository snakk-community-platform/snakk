using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class UserRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly UserRepository _repository;

    public UserRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new UserRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetForUpdateAsync Tests

    [Test]
    public async Task GetForUpdateAsync_ExistingUser_ReturnsEntity()
    {
        var user = await _builder.CreateUserAsync("TestUser", "test@example.com");

        var result = await _repository.GetForUpdateAsync(user.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(user.PublicId);
        await Assert.That(result.DisplayName).IsEqualTo("TestUser");
        await Assert.That(result.Email).IsEqualTo("test@example.com");
    }

    [Test]
    public async Task GetForUpdateAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetForUpdateAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetForDisplayAsync Tests

    [Test]
    public async Task GetForDisplayAsync_ExistingUser_ReturnsDto()
    {
        var user = await _builder.CreateUserAsync("DisplayUser", "display@example.com");

        var result = await _repository.GetForDisplayAsync(user.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(user.PublicId);
        await Assert.That(result.DisplayName).IsEqualTo("DisplayUser");
        await Assert.That(result.Email).IsEqualTo("display@example.com");
        await Assert.That(result.CreatedAt).IsEqualTo(user.CreatedAt);
    }

    [Test]
    public async Task GetForDisplayAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetForDisplayAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingUser_ReturnsEntity()
    {
        var user = await _builder.CreateUserAsync("PublicIdUser");

        var result = await _repository.GetByPublicIdAsync(user.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(user.PublicId);
        await Assert.That(result.DisplayName).IsEqualTo("PublicIdUser");
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdsAsync Tests

    [Test]
    public async Task GetByPublicIdsAsync_MultipleValidIds_ReturnsAllMatching()
    {
        var user1 = await _builder.CreateUserAsync("User One");
        var user2 = await _builder.CreateUserAsync("User Two");
        var user3 = await _builder.CreateUserAsync("User Three");

        var result = (await _repository.GetByPublicIdsAsync(
            new[] { user1.PublicId, user2.PublicId, user3.PublicId })).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetByPublicIdsAsync_SomeValidSomeInvalid_ReturnsOnlyMatching()
    {
        var user1 = await _builder.CreateUserAsync("Valid User");
        var user2 = await _builder.CreateUserAsync("Another Valid User");

        var result = (await _repository.GetByPublicIdsAsync(
            new[] { user1.PublicId, "nonexistent_id_1", user2.PublicId, "nonexistent_id_2" })).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetByPublicIdsAsync_EmptyList_ReturnsEmpty()
    {
        var result = (await _repository.GetByPublicIdsAsync(
            [])).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetByEmailAsync Tests

    [Test]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsEntity()
    {
        var user = await _builder.CreateUserAsync("EmailUser", "findme@example.com");

        var result = await _repository.GetByEmailAsync("findme@example.com");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Email).IsEqualTo("findme@example.com");
        await Assert.That(result.DisplayName).IsEqualTo("EmailUser");
    }

    [Test]
    public async Task GetByEmailAsync_NonExistentEmail_ReturnsNull()
    {
        var result = await _repository.GetByEmailAsync("doesnotexist@example.com");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByDisplayNameAsync Tests

    [Test]
    public async Task GetByDisplayNameAsync_ExactMatch_ReturnsEntity()
    {
        var user = await _builder.CreateUserAsync("UniqueDisplayName");

        var result = await _repository.GetByDisplayNameAsync("UniqueDisplayName");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DisplayName).IsEqualTo("UniqueDisplayName");
    }

    [Test]
    public async Task GetByDisplayNameAsync_DifferentCase_ReturnsEntity()
    {
        var user = await _builder.CreateUserAsync("CaseSensitiveUser");

        var result = await _repository.GetByDisplayNameAsync("casesensitiveuser");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DisplayName).IsEqualTo("CaseSensitiveUser");
    }

    [Test]
    public async Task GetByDisplayNameAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetByDisplayNameAsync("NonExistentDisplayName");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region SearchByDisplayNameAsync Tests

    [Test]
    public async Task SearchByDisplayNameAsync_PartialMatch_ReturnsMatchingUsers()
    {
        await _builder.CreateUserAsync("JohnSmith");
        await _builder.CreateUserAsync("JohnDoe");
        await _builder.CreateUserAsync("JaneDoe");

        var result = (await _repository.SearchByDisplayNameAsync("John", 10)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SearchByDisplayNameAsync_RespectsLimit()
    {
        await _builder.CreateUserAsync("SearchUser Alpha");
        await _builder.CreateUserAsync("SearchUser Beta");
        await _builder.CreateUserAsync("SearchUser Gamma");
        await _builder.CreateUserAsync("SearchUser Delta");

        var result = (await _repository.SearchByDisplayNameAsync("SearchUser", 2)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SearchByDisplayNameAsync_CaseInsensitive_ReturnsMatches()
    {
        await _builder.CreateUserAsync("AlphaUser");
        await _builder.CreateUserAsync("ALPHAADMIN");

        var result = (await _repository.SearchByDisplayNameAsync("alpha", 10)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SearchByDisplayNameAsync_NoMatch_ReturnsEmpty()
    {
        await _builder.CreateUserAsync("SomeUser");

        var result = (await _repository.SearchByDisplayNameAsync("zzzznonexistent", 10)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion
}
