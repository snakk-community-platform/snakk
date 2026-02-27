using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class UserAchievementProgressRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly UserAchievementProgressRepository _repository;

    public UserAchievementProgressRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new UserAchievementProgressRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingProgress_ReturnsWithAchievementAndUserLoaded()
    {
        var user = await _builder.CreateUserAsync("ProgressUser");
        var achievement = await _builder.CreateAchievementAsync("Post Count");
        var progress = await _builder.CreateAchievementProgressAsync(user.Id, achievement.Id, currentValue: 5, targetValue: 10);

        var result = await _repository.GetByIdAsync(progress.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(progress.Id);
        await Assert.That(result.CurrentValue).IsEqualTo(5);
        await Assert.That(result.TargetValue).IsEqualTo(10);
        await Assert.That(result.Achievement).IsNotNull();
        await Assert.That(result.Achievement.Name).IsEqualTo("Post Count");
        await Assert.That(result.User).IsNotNull();
        await Assert.That(result.User.DisplayName).IsEqualTo("ProgressUser");
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByUserAndAchievementAsync Tests

    [Test]
    public async Task GetByUserAndAchievementAsync_ExistingProgress_ReturnsWithAchievementLoaded()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("Reply Master");
        var progress = await _builder.CreateAchievementProgressAsync(user.Id, achievement.Id, currentValue: 3, targetValue: 20);

        var result = await _repository.GetByUserAndAchievementAsync(user.Id, achievement.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.UserId).IsEqualTo(user.Id);
        await Assert.That(result.AchievementId).IsEqualTo(achievement.Id);
        await Assert.That(result.CurrentValue).IsEqualTo(3);
        await Assert.That(result.Achievement).IsNotNull();
        await Assert.That(result.Achievement.Name).IsEqualTo("Reply Master");
    }

    [Test]
    public async Task GetByUserAndAchievementAsync_NonExistentCombination_ReturnsNull()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("No Progress");

        var result = await _repository.GetByUserAndAchievementAsync(user.Id, achievement.Id);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Test]
    public async Task GetByUserIdAsync_MultipleProgressRecords_ReturnsAllForUser()
    {
        var user = await _builder.CreateUserAsync();
        var ach1 = await _builder.CreateAchievementAsync("Achievement 1");
        var ach2 = await _builder.CreateAchievementAsync("Achievement 2");
        var ach3 = await _builder.CreateAchievementAsync("Achievement 3");

        await _builder.CreateAchievementProgressAsync(user.Id, ach1.Id, currentValue: 2, targetValue: 10);
        await _builder.CreateAchievementProgressAsync(user.Id, ach2.Id, currentValue: 7, targetValue: 15);
        await _builder.CreateAchievementProgressAsync(user.Id, ach3.Id, currentValue: 0, targetValue: 5);

        var result = (await _repository.GetByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetByUserIdAsync_IncludesAchievementNavigationProperty()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("Discussion Starter");
        await _builder.CreateAchievementProgressAsync(user.Id, achievement.Id, currentValue: 1, targetValue: 5);

        var result = (await _repository.GetByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Achievement).IsNotNull();
        await Assert.That(result[0].Achievement.Name).IsEqualTo("Discussion Starter");
    }

    [Test]
    public async Task GetByUserIdAsync_NoProgress_ReturnsEmpty()
    {
        var user = await _builder.CreateUserAsync();

        var result = (await _repository.GetByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetIncompleteByUserIdAsync Tests

    [Test]
    public async Task GetIncompleteByUserIdAsync_MixOfCompleteAndIncomplete_ReturnsOnlyIncomplete()
    {
        var user = await _builder.CreateUserAsync();
        var ach1 = await _builder.CreateAchievementAsync("Complete Achievement");
        var ach2 = await _builder.CreateAchievementAsync("Incomplete Achievement");
        var ach3 = await _builder.CreateAchievementAsync("Another Incomplete");

        // Complete (should be excluded)
        await _builder.CreateAchievementProgressAsync(user.Id, ach1.Id, currentValue: 10, targetValue: 10);
        // Incomplete (should be included)
        await _builder.CreateAchievementProgressAsync(user.Id, ach2.Id, currentValue: 5, targetValue: 10);
        // Incomplete (should be included)
        await _builder.CreateAchievementProgressAsync(user.Id, ach3.Id, currentValue: 0, targetValue: 10);

        var result = (await _repository.GetIncompleteByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(p => p.CurrentValue < p.TargetValue)).IsTrue();
        await Assert.That(result.Any(p => p.AchievementId == ach2.Id)).IsTrue();
        await Assert.That(result.Any(p => p.AchievementId == ach3.Id)).IsTrue();
    }

    [Test]
    public async Task GetIncompleteByUserIdAsync_IncludesAchievementNavigationProperty()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("In Progress");
        await _builder.CreateAchievementProgressAsync(user.Id, achievement.Id, currentValue: 3, targetValue: 10);

        var result = (await _repository.GetIncompleteByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Achievement).IsNotNull();
        await Assert.That(result[0].Achievement.Name).IsEqualTo("In Progress");
    }

    #endregion
}
