using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class UserAchievementRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly UserAchievementRepository _repository;

    public UserAchievementRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new UserAchievementRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingUserAchievement_ReturnsWithAchievementAndUserLoaded()
    {
        var user = await _builder.CreateUserAsync("TestUser");
        var achievement = await _builder.CreateAchievementAsync("First Post");
        var userAchievement = await _builder.CreateUserAchievementAsync(user.Id, achievement.Id);

        var result = await _repository.GetByIdAsync(userAchievement.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(userAchievement.Id);
        await Assert.That(result.Achievement).IsNotNull();
        await Assert.That(result.Achievement.Name).IsEqualTo("First Post");
        await Assert.That(result.User).IsNotNull();
        await Assert.That(result.User.DisplayName).IsEqualTo("TestUser");
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
    public async Task GetByPublicIdAsync_ExistingUserAchievement_ReturnsEntity()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("Veteran");
        var userAchievement = await _builder.CreateUserAchievementAsync(user.Id, achievement.Id);

        var result = await _repository.GetByPublicIdAsync(userAchievement.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(userAchievement.PublicId);
        await Assert.That(result.Achievement).IsNotNull();
        await Assert.That(result.User).IsNotNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("ua_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Test]
    public async Task GetByUserIdAsync_MultipleAchievements_ReturnsAllOrderedByEarnedAtDescending()
    {
        var user = await _builder.CreateUserAsync();
        var ach1 = await _builder.CreateAchievementAsync("First");
        var ach2 = await _builder.CreateAchievementAsync("Second");
        var ach3 = await _builder.CreateAchievementAsync("Third");

        var ua1 = await _builder.CreateUserAchievementAsync(user.Id, ach1.Id);
        ua1.EarnedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ua2 = await _builder.CreateUserAchievementAsync(user.Id, ach2.Id);
        ua2.EarnedAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var ua3 = await _builder.CreateUserAchievementAsync(user.Id, ach3.Id);
        ua3.EarnedAt = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].AchievementId).IsEqualTo(ach2.Id); // March - newest
        await Assert.That(result[1].AchievementId).IsEqualTo(ach3.Id); // February
        await Assert.That(result[2].AchievementId).IsEqualTo(ach1.Id); // January - oldest
    }

    [Test]
    public async Task GetByUserIdAsync_IncludesAchievementNavigationProperty()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("Helpful");
        await _builder.CreateUserAchievementAsync(user.Id, achievement.Id);

        var result = (await _repository.GetByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Achievement).IsNotNull();
        await Assert.That(result[0].Achievement.Name).IsEqualTo("Helpful");
    }

    [Test]
    public async Task GetByUserIdAsync_NoAchievements_ReturnsEmpty()
    {
        var user = await _builder.CreateUserAsync();

        var result = (await _repository.GetByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetDisplayedByUserIdAsync Tests

    [Test]
    public async Task GetDisplayedByUserIdAsync_MixOfDisplayedAndNotDisplayed_ReturnsOnlyDisplayed()
    {
        var user = await _builder.CreateUserAsync();
        var ach1 = await _builder.CreateAchievementAsync("Displayed One");
        var ach2 = await _builder.CreateAchievementAsync("Not Displayed");
        var ach3 = await _builder.CreateAchievementAsync("Displayed Two");

        var ua1 = await _builder.CreateUserAchievementAsync(user.Id, ach1.Id);
        ua1.IsDisplayed = true;
        ua1.DisplayOrder = 1;

        var ua2 = await _builder.CreateUserAchievementAsync(user.Id, ach2.Id);
        // ua2 remains IsDisplayed = false (default)

        var ua3 = await _builder.CreateUserAchievementAsync(user.Id, ach3.Id);
        ua3.IsDisplayed = true;
        ua3.DisplayOrder = 2;

        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetDisplayedByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.Any(r => r.Id == ua1.Id)).IsTrue();
        await Assert.That(result.Any(r => r.Id == ua3.Id)).IsTrue();
        await Assert.That(result.Any(r => r.Id == ua2.Id)).IsFalse();
    }

    [Test]
    public async Task GetDisplayedByUserIdAsync_CorrectOrderingByDisplayOrder()
    {
        var user = await _builder.CreateUserAsync();
        var ach1 = await _builder.CreateAchievementAsync("Second Displayed");
        var ach2 = await _builder.CreateAchievementAsync("First Displayed");

        var ua1 = await _builder.CreateUserAchievementAsync(user.Id, ach1.Id);
        ua1.IsDisplayed = true;
        ua1.DisplayOrder = 2;

        var ua2 = await _builder.CreateUserAchievementAsync(user.Id, ach2.Id);
        ua2.IsDisplayed = true;
        ua2.DisplayOrder = 1;

        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetDisplayedByUserIdAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].DisplayOrder).IsEqualTo(1);
        await Assert.That(result[1].DisplayOrder).IsEqualTo(2);
    }

    #endregion

    #region HasAchievementAsync Tests

    [Test]
    public async Task HasAchievementAsync_UserHasAchievement_ReturnsTrue()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("Earned");
        await _builder.CreateUserAchievementAsync(user.Id, achievement.Id);

        var result = await _repository.HasAchievementAsync(user.Id, achievement.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasAchievementAsync_UserDoesNotHaveAchievement_ReturnsFalse()
    {
        var user = await _builder.CreateUserAsync();
        var achievement = await _builder.CreateAchievementAsync("Not Earned");

        var result = await _repository.HasAchievementAsync(user.Id, achievement.Id);

        await Assert.That(result).IsFalse();
    }

    #endregion
}
