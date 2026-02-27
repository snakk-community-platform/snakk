using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Domain.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Adapters;

public class AchievementRepositoryAdapterTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly AchievementRepositoryAdapter _adapter;

    public AchievementRepositoryAdapterTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        var databaseRepo = new AchievementRepository(_db.Context);
        _adapter = new AchievementRepositoryAdapter(databaseRepo, _db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingAchievement_ReturnsMappedDomainEntity()
    {
        var dbEntity = await _builder.CreateAchievementAsync("First Post", "first-post", categoryId: 1, tierLevel: 1);

        var result = await _adapter.GetByPublicIdAsync(AchievementId.From(dbEntity.PublicId));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId.Value).IsEqualTo(dbEntity.PublicId);
        await Assert.That(result.Name).IsEqualTo("First Post");
        await Assert.That(result.Slug).IsEqualTo("first-post");
        await Assert.That(result.IsActive).IsTrue();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _adapter.GetByPublicIdAsync(AchievementId.From("ach_nonexistent"));

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetBySlugAsync Tests

    [Test]
    public async Task GetBySlugAsync_ExistingAchievement_ReturnsMappedDomainEntity()
    {
        var dbEntity = await _builder.CreateAchievementAsync("Early Adopter", "early-adopter");

        var result = await _adapter.GetBySlugAsync("early-adopter");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Slug).IsEqualTo("early-adopter");
        await Assert.That(result.Name).IsEqualTo("Early Adopter");
        await Assert.That(result.PublicId.Value).IsEqualTo(dbEntity.PublicId);
    }

    [Test]
    public async Task GetBySlugAsync_NonExistentSlug_ReturnsNull()
    {
        var result = await _adapter.GetBySlugAsync("no-such-slug");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllActiveAsync Tests

    [Test]
    public async Task GetAllActiveAsync_MixedActiveAndInactive_ReturnsOnlyActiveMappedEntities()
    {
        await _builder.CreateAchievementAsync("Active One", isActive: true);
        await _builder.CreateAchievementAsync("Active Two", isActive: true);
        await _builder.CreateAchievementAsync("Inactive One", isActive: false);

        var result = (await _adapter.GetAllActiveAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(a => a.IsActive)).IsTrue();
        await Assert.That(result.Any(a => a.Name == "Active One")).IsTrue();
        await Assert.That(result.Any(a => a.Name == "Active Two")).IsTrue();
    }

    #endregion

    #region GetByCategoryAsync Tests

    [Test]
    public async Task GetByCategoryAsync_FiltersByCategory_ReturnsMappedEntities()
    {
        await _builder.CreateAchievementAsync("Engagement Ach", categoryId: (int)AchievementCategoryEnum.Engagement);
        await _builder.CreateAchievementAsync("Social Ach", categoryId: (int)AchievementCategoryEnum.Social);

        var result = (await _adapter.GetByCategoryAsync(AchievementCategoryEnum.Engagement)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("Engagement Ach");
        await Assert.That(result[0].Category).IsEqualTo(AchievementCategoryEnum.Engagement);
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_NonExistentPublicId_ThrowsInvalidOperationException()
    {
        var domainEntity = Achievement.Create(
            slug: "ghost",
            name: "Ghost Achievement",
            description: "Does not exist",
            iconUrl: null,
            category: AchievementCategoryEnum.Engagement,
            tier: AchievementTierEnum.Bronze,
            points: 10,
            isSecret: false,
            requirementType: AchievementRequirementTypeEnum.Count,
            requirementConfig: "{}",
            displayOrder: 0);

        await Assert.That(async () => await _adapter.UpdateAsync(domainEntity))
            .Throws<InvalidOperationException>();
    }

    #endregion
}
