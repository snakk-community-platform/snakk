namespace Snakk.Application.Services;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public class AchievementService(
    IAchievementRepository achievementRepository,
    IUserAchievementRepository userAchievementRepository,
    IUserAchievementProgressRepository userAchievementProgressRepository)
{
    // ==================== Achievement Queries ====================

    /// <summary>
    /// Get all active achievements
    /// </summary>
    public async Task<IEnumerable<Achievement>> GetAllActiveAchievementsAsync() =>
        await achievementRepository.GetAllActiveAsync();

    /// <summary>
    /// Get achievement by slug
    /// </summary>
    public async Task<Achievement?> GetAchievementBySlugAsync(string slug) =>
        await achievementRepository.GetBySlugAsync(slug);

    /// <summary>
    /// Get achievements by category
    /// </summary>
    public async Task<IEnumerable<Achievement>> GetAchievementsByCategoryAsync(AchievementCategoryEnum category) =>
        await achievementRepository.GetByCategoryAsync(category);

    // ==================== User Achievement Queries ====================

    /// <summary>
    /// Get all achievements earned by a user
    /// </summary>
    public async Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(UserId userId) =>
        await userAchievementRepository.GetByUserIdAsync(userId);

    /// <summary>
    /// Get displayed achievements for a user (for profile showcase)
    /// </summary>
    public async Task<IEnumerable<UserAchievement>> GetDisplayedUserAchievementsAsync(UserId userId) =>
        await userAchievementRepository.GetDisplayedByUserIdAsync(userId);

    /// <summary>
    /// Check if user has earned a specific achievement
    /// </summary>
    public async Task<bool> HasAchievementAsync(UserId userId, AchievementId achievementId) =>
        await userAchievementRepository.HasAchievementAsync(userId, achievementId);

    // ==================== User Progress Queries ====================

    /// <summary>
    /// Get all achievement progress for a user
    /// </summary>
    public async Task<IEnumerable<UserAchievementProgress>> GetUserProgressAsync(UserId userId) =>
        await userAchievementProgressRepository.GetByUserIdAsync(userId);

    /// <summary>
    /// Get incomplete achievement progress for a user
    /// </summary>
    public async Task<IEnumerable<UserAchievementProgress>> GetIncompleteUserProgressAsync(UserId userId) =>
        await userAchievementProgressRepository.GetIncompleteByUserIdAsync(userId);

    /// <summary>
    /// Get progress for a specific achievement
    /// </summary>
    public async Task<UserAchievementProgress?> GetProgressAsync(UserId userId, AchievementId achievementId) =>
        await userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);

    // ==================== Manual Award (Phase 1) ====================

    /// <summary>
    /// Manually award an achievement to a user (Phase 1 - no automatic triggering)
    /// </summary>
    public async Task AwardAchievementAsync(UserId userId, AchievementId achievementId)
    {
        // Check if user already has this achievement
        var alreadyHas = await userAchievementRepository.HasAchievementAsync(userId, achievementId);

        if (alreadyHas)
            throw new InvalidOperationException($"User '{userId}' already has achievement '{achievementId}'");

        // Verify achievement exists
        var achievement = await achievementRepository.GetByPublicIdAsync(achievementId);

        if (achievement is null)
            throw new InvalidOperationException($"Achievement '{achievementId}' not found");

        // Create user achievement
        var userAchievement = UserAchievement.Create(userId, achievementId);
        await userAchievementRepository.AddAsync(userAchievement);

        // Clean up progress tracking if it exists
        var progress = await userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);

        if (progress is not null)
            await userAchievementProgressRepository.DeleteAsync(progress);
    }

    // ==================== Progress Tracking (Manual in Phase 1) ====================

    /// <summary>
    /// Initialize progress tracking for an achievement (Phase 1 - manual)
    /// </summary>
    public async Task InitializeProgressAsync(UserId userId, AchievementId achievementId, int targetValue)
    {
        // Check if progress already exists
        var existing = await userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);

        if (existing is not null)
        {
            throw new InvalidOperationException($"Progress already exists for user '{userId}' and achievement '{achievementId}'");
        }

        // Verify achievement exists
        var achievement = await achievementRepository.GetByPublicIdAsync(achievementId);

        if (achievement is null)
        {
            throw new InvalidOperationException($"Achievement '{achievementId}' not found");
        }

        // Create progress
        var progress = UserAchievementProgress.Create(userId, achievementId, targetValue);
        await userAchievementProgressRepository.AddAsync(progress);
    }

    /// <summary>
    /// Update progress for an achievement (Phase 1 - manual)
    /// In Phase 2+, this will be called automatically by event handlers
    /// </summary>
    public async Task UpdateProgressAsync(UserId userId, AchievementId achievementId, int currentValue, string? progressData = null)
    {
        var progress = await userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);

        if (progress is null)
        {
            throw new InvalidOperationException($"Progress not found for user '{userId}' and achievement '{achievementId}'");
        }

        // Update progress
        progress.UpdateProgress(currentValue, progressData);

        // Check if achievement is now complete
        if (progress.IsComplete())
        {
            // Auto-award the achievement
            var alreadyHas = await userAchievementRepository.HasAchievementAsync(userId, achievementId);

            if (!alreadyHas)
            {
                var userAchievement = UserAchievement.Create(userId, achievementId);
                await userAchievementRepository.AddAsync(userAchievement);
            }

            // Delete progress since achievement is earned
            await userAchievementProgressRepository.DeleteAsync(progress);
        }
        else
        {
            // Save progress
            await userAchievementProgressRepository.UpdateAsync(progress);
        }
    }

    // ==================== Display Management ====================

    /// <summary>
    /// Update achievement display settings (for profile showcase)
    /// </summary>
    public async Task UpdateAchievementDisplayAsync(UserAchievementId userAchievementId, bool isDisplayed, int displayOrder)
    {
        var userAchievement = await userAchievementRepository.GetByPublicIdAsync(userAchievementId);

        if (userAchievement is null)
        {
            throw new InvalidOperationException($"UserAchievement '{userAchievementId}' not found");
        }

        userAchievement.UpdateDisplay(isDisplayed, displayOrder);
        await userAchievementRepository.UpdateAsync(userAchievement);
    }
}
