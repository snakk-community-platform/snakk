namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public class UserMetric
{
    public UserId UserId { get; private set; }
    public string MetricType { get; private set; }
    public MetricScopeEnum Scope { get; private set; }
    public int? ScopeId { get; private set; }
    public int Value { get; private set; }
    public DateTime LastUpdated { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private UserMetric() { }
#pragma warning restore CS8618

    private UserMetric(
        UserId userId,
        string metricType,
        MetricScopeEnum scope,
        int? scopeId,
        int value,
        DateTime lastUpdated)
    {
        UserId = userId;
        MetricType = metricType;
        Scope = scope;
        ScopeId = scopeId;
        Value = value;
        LastUpdated = lastUpdated;
    }

    public static UserMetric Create(
        UserId userId,
        string metricType,
        MetricScopeEnum scope,
        int? scopeId = null)
    {
        if (string.IsNullOrWhiteSpace(metricType))
            throw new ArgumentException("MetricType cannot be empty", nameof(metricType));

        if (scope != MetricScopeEnum.Global && scopeId == null)
            throw new ArgumentException("ScopeId is required for non-global scopes", nameof(scopeId));

        return new UserMetric(
            userId,
            metricType,
            scope,
            scopeId,
            value: 0,
            DateTime.UtcNow);
    }

    public static UserMetric Rehydrate(
        UserId userId,
        string metricType,
        MetricScopeEnum scope,
        int? scopeId,
        int value,
        DateTime lastUpdated)
    {
        return new UserMetric(
            userId,
            metricType,
            scope,
            scopeId,
            value,
            lastUpdated);
    }

    public void Increment(int amount = 1)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        Value += amount;
        LastUpdated = DateTime.UtcNow;
    }

    public void SetValue(int newValue)
    {
        if (newValue < 0)
            throw new ArgumentException("Value cannot be negative", nameof(newValue));

        Value = newValue;
        LastUpdated = DateTime.UtcNow;
    }
}
