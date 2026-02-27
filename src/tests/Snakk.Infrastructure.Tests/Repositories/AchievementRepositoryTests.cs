using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class AchievementRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly AchievementRepository _repository;

    public AchievementRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new AchievementRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingAchievement_ReturnsEntity()
    {
        var achievement = await _builder.CreateAchievementAsync("First Post", "first-post");

        var result = await _repository.GetByIdAsync(achievement.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(achievement.Id);
        await Assert.That(result.Name).IsEqualTo("First Post");
        await Assert.That(result.Slug).IsEqualTo("first-post");
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingAchievement_ReturnsEntity()
    {
        var achievement = await _builder.CreateAchievementAsync("Veteran");

        var result = await _repository.GetByPublicIdAsync(achievement.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(achievement.PublicId);
        await Assert.That(result.Name).IsEqualTo("Veteran");
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("ach_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetBySlugAsync Tests

    [Test]
    public async Task GetBySlugAsync_ExistingAchievement_ReturnsEntity()
    {
        var achievement = await _builder.CreateAchievementAsync("Early Adopter", "early-adopter");

        var result = await _repository.GetBySlugAsync("early-adopter");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Slug).IsEqualTo("early-adopter");
        await Assert.That(result.Name).IsEqualTo("Early Adopter");
    }

    [Test]
    public async Task GetBySlugAsync_NonExistentSlug_ReturnsNull()
    {
        var result = await _repository.GetBySlugAsync("no-such-achievement");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllActiveAsync Tests

    [Test]
    public async Task GetAllActiveAsync_MixOfActiveAndInactive_ReturnsOnlyActive()
    {
        var active1 = await _builder.CreateAchievementAsync("Active One", isActive: true);
        var active2 = await _builder.CreateAchievementAsync("Active Two", isActive: true);
        var inactive = await _builder.CreateAchievementAsync("Inactive One", isActive: false);

        var result = (await _repository.GetAllActiveAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.Any(a => a.Id == active1.Id)).IsTrue();
        await Assert.That(result.Any(a => a.Id == active2.Id)).IsTrue();
        await Assert.That(result.Any(a => a.Id == inactive.Id)).IsFalse();
    }

    [Test]
    public async Task GetAllActiveAsync_OrderedByDisplayOrder()
    {
        var ach1 = await _builder.CreateAchievementAsync("Third");
        ach1.DisplayOrder = 3;
        var ach2 = await _builder.CreateAchievementAsync("First");
        ach2.DisplayOrder = 1;
        var ach3 = await _builder.CreateAchievementAsync("Second");
        ach3.DisplayOrder = 2;
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetAllActiveAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].Name).IsEqualTo("First");
        await Assert.That(result[1].Name).IsEqualTo("Second");
        await Assert.That(result[2].Name).IsEqualTo("Third");
    }

    #endregion

    #region GetByCategoryIdAsync Tests

    [Test]
    public async Task GetByCategoryIdAsync_ReturnsOnlyForSpecifiedCategory()
    {
        var cat1Ach = await _builder.CreateAchievementAsync("Cat1 Achievement", categoryId: 1);
        var cat2Ach = await _builder.CreateAchievementAsync("Cat2 Achievement", categoryId: 2);

        var result = (await _repository.GetByCategoryIdAsync(1)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo(cat1Ach.Id);
        await Assert.That(result[0].CategoryId).IsEqualTo(1);
    }

    [Test]
    public async Task GetByCategoryIdAsync_ExcludesInactiveEvenInCorrectCategory()
    {
        var active = await _builder.CreateAchievementAsync("Active", categoryId: 1, isActive: true);
        var inactive = await _builder.CreateAchievementAsync("Inactive", categoryId: 1, isActive: false);

        var result = (await _repository.GetByCategoryIdAsync(1)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo(active.Id);
        await Assert.That(result.Any(a => a.Id == inactive.Id)).IsFalse();
    }

    #endregion
}
