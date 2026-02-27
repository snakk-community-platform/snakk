using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Domain.Tests.Entities;

public class UserMetricTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithGlobalScope_WithoutScopeId_Succeeds()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var metric = UserMetric.Create(userId, "post_count", MetricScopeEnum.Global);

        // Assert
        await Assert.That(metric).IsNotNull();
        await Assert.That(metric.UserId).IsEqualTo(userId);
        await Assert.That(metric.MetricType).IsEqualTo("post_count");
        await Assert.That(metric.Scope).IsEqualTo(MetricScopeEnum.Global);
        await Assert.That(metric.ScopeId).IsNull();
        await Assert.That(metric.Value).IsEqualTo(0);
        await Assert.That(metric.LastUpdated).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_WithNonGlobalScope_WithScopeId_Succeeds()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        var metric = UserMetric.Create(userId, "post_count", MetricScopeEnum.Community, scopeId: 42);

        // Assert
        await Assert.That(metric.Scope).IsEqualTo(MetricScopeEnum.Community);
        await Assert.That(metric.ScopeId).IsEqualTo(42);
    }

    [Test]
    public async Task Create_WithNonGlobalScope_WithoutScopeId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => UserMetric.Create(
            UserId.New(), "post_count", MetricScopeEnum.Community)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithHubScope_WithoutScopeId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => UserMetric.Create(
            UserId.New(), "post_count", MetricScopeEnum.Hub)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithSpaceScope_WithoutScopeId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => UserMetric.Create(
            UserId.New(), "post_count", MetricScopeEnum.Space)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptyMetricType_ThrowsArgumentException(string? emptyType)
    {
        // Act & Assert
        await Assert.That(() => UserMetric.Create(
            UserId.New(), emptyType!, MetricScopeEnum.Global)).Throws<ArgumentException>();
    }

    #endregion

    #region Increment Tests

    [Test]
    public async Task Increment_IncrementsValue()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act
        metric.Increment();

        // Assert
        await Assert.That(metric.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Increment_WithAmount_IncrementsByAmount()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act
        metric.Increment(5);

        // Assert
        await Assert.That(metric.Value).IsEqualTo(5);
    }

    [Test]
    public async Task Increment_MultipleIncrements_Accumulate()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act
        metric.Increment(3);
        metric.Increment(7);

        // Assert
        await Assert.That(metric.Value).IsEqualTo(10);
    }

    [Test]
    public async Task Increment_WithNegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act & Assert
        await Assert.That(() => metric.Increment(-1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Increment_UpdatesLastUpdated()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act
        metric.Increment();

        // Assert
        await Assert.That(metric.LastUpdated).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    #endregion

    #region SetValue Tests

    [Test]
    public async Task SetValue_SetsValue()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act
        metric.SetValue(42);

        // Assert
        await Assert.That(metric.Value).IsEqualTo(42);
    }

    [Test]
    public async Task SetValue_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act & Assert
        await Assert.That(() => metric.SetValue(-1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task SetValue_WithZero_Succeeds()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);
        metric.Increment(10);

        // Act
        metric.SetValue(0);

        // Assert
        await Assert.That(metric.Value).IsEqualTo(0);
    }

    [Test]
    public async Task SetValue_UpdatesLastUpdated()
    {
        // Arrange
        var metric = UserMetric.Create(UserId.New(), "post_count", MetricScopeEnum.Global);

        // Act
        metric.SetValue(100);

        // Assert
        await Assert.That(metric.LastUpdated).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var userId = UserId.New();
        var lastUpdated = DateTime.UtcNow.AddDays(-3);

        // Act
        var metric = UserMetric.Rehydrate(
            userId, "reaction_count", MetricScopeEnum.Hub, 15, 99, lastUpdated);

        // Assert
        await Assert.That(metric.UserId).IsEqualTo(userId);
        await Assert.That(metric.MetricType).IsEqualTo("reaction_count");
        await Assert.That(metric.Scope).IsEqualTo(MetricScopeEnum.Hub);
        await Assert.That(metric.ScopeId).IsEqualTo(15);
        await Assert.That(metric.Value).IsEqualTo(99);
        await Assert.That(metric.LastUpdated).IsEqualTo(lastUpdated);
    }

    #endregion
}
