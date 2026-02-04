namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class UserAchievementProgress
{
    public UserId UserId { get; private set; }
    public AchievementId AchievementId { get; private set; }
    public int CurrentValue { get; private set; }
    public int TargetValue { get; private set; }
    public string? ProgressData { get; private set; } // JSON for additional tracking data
    public DateTime LastUpdated { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private UserAchievementProgress() { }
#pragma warning restore CS8618

    private UserAchievementProgress(
        UserId userId,
        AchievementId achievementId,
        int currentValue,
        int targetValue,
        string? progressData,
        DateTime lastUpdated)
    {
        UserId = userId;
        AchievementId = achievementId;
        CurrentValue = currentValue;
        TargetValue = targetValue;
        ProgressData = progressData;
        LastUpdated = lastUpdated;
    }

    public static UserAchievementProgress Create(
        UserId userId,
        AchievementId achievementId,
        int targetValue,
        string? progressData = null)
    {
        if (targetValue <= 0)
            throw new ArgumentException("Target value must be positive", nameof(targetValue));

        return new UserAchievementProgress(
            userId,
            achievementId,
            currentValue: 0,
            targetValue,
            progressData,
            DateTime.UtcNow);
    }

    public static UserAchievementProgress Rehydrate(
        UserId userId,
        AchievementId achievementId,
        int currentValue,
        int targetValue,
        string? progressData,
        DateTime lastUpdated)
    {
        return new UserAchievementProgress(
            userId,
            achievementId,
            currentValue,
            targetValue,
            progressData,
            lastUpdated);
    }

    public void UpdateProgress(int currentValue, string? progressData = null)
    {
        if (currentValue < 0)
            throw new ArgumentException("Current value cannot be negative", nameof(currentValue));

        CurrentValue = currentValue;
        if (progressData != null)
        {
            ProgressData = progressData;
        }
        LastUpdated = DateTime.UtcNow;
    }

    public void IncrementProgress(int increment = 1, string? progressData = null)
    {
        UpdateProgress(CurrentValue + increment, progressData);
    }

    public bool IsComplete() => CurrentValue >= TargetValue;

    public double GetProgressPercentage() => TargetValue > 0 ? (double)CurrentValue / TargetValue * 100 : 0;
}
