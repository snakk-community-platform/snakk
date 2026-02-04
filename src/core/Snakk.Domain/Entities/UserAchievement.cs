namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class UserAchievement
{
    public UserAchievementId PublicId { get; private set; }
    public UserId UserId { get; private set; }
    public AchievementId AchievementId { get; private set; }
    public DateTime EarnedAt { get; private set; }
    public bool IsDisplayed { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool NotificationSent { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private UserAchievement() { }
#pragma warning restore CS8618

    private UserAchievement(
        UserAchievementId publicId,
        UserId userId,
        AchievementId achievementId,
        DateTime earnedAt,
        bool isDisplayed,
        int displayOrder,
        bool notificationSent)
    {
        PublicId = publicId;
        UserId = userId;
        AchievementId = achievementId;
        EarnedAt = earnedAt;
        IsDisplayed = isDisplayed;
        DisplayOrder = displayOrder;
        NotificationSent = notificationSent;
    }

    public static UserAchievement Create(
        UserId userId,
        AchievementId achievementId)
    {
        return new UserAchievement(
            UserAchievementId.New(),
            userId,
            achievementId,
            DateTime.UtcNow,
            isDisplayed: false,
            displayOrder: 0,
            notificationSent: false);
    }

    public static UserAchievement Rehydrate(
        UserAchievementId publicId,
        UserId userId,
        AchievementId achievementId,
        DateTime earnedAt,
        bool isDisplayed,
        int displayOrder,
        bool notificationSent)
    {
        return new UserAchievement(
            publicId,
            userId,
            achievementId,
            earnedAt,
            isDisplayed,
            displayOrder,
            notificationSent);
    }

    public void UpdateDisplay(bool isDisplayed, int displayOrder)
    {
        IsDisplayed = isDisplayed;
        DisplayOrder = displayOrder;
    }

    public void MarkNotificationSent()
    {
        NotificationSent = true;
    }
}
