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
    private readonly IAchievementRepository _achievementRepository = achievementRepository;
    private readonly IUserAchievementRepository _userAchievementRepository = userAchievementRepository;
    private readonly IUserAchievementProgressRepository _userAchievementProgressRepository = userAchievementProgressRepository;

    // ==================== Achievement Queries ====================

    /// <summary>
    /// Get all active achievements
    /// </summary>
    public async Task<IEnumerable<Achievement>> GetAllActiveAchievementsAsync()
    {
        return await _achievementRepository.GetAllActiveAsync();
    }

    /// <summary>
    /// Get achievement by slug
    /// </summary>
    public async Task<Achievement?> GetAchievementBySlugAsync(string slug)
    {
        return await _achievementRepository.GetBySlugAsync(slug);
    }

    /// <summary>
    /// Get achievements by category
    /// </summary>
    public async Task<IEnumerable<Achievement>> GetAchievementsByCategoryAsync(AchievementCategoryEnum category)
    {
        return await _achievementRepository.GetByCategoryAsync(category);
    }

    // ==================== User Achievement Queries ====================

    /// <summary>
    /// Get all achievements earned by a user
    /// </summary>
    public async Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(UserId userId)
    {
        return await _userAchievementRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Get displayed achievements for a user (for profile showcase)
    /// </summary>
    public async Task<IEnumerable<UserAchievement>> GetDisplayedUserAchievementsAsync(UserId userId)
    {
        return await _userAchievementRepository.GetDisplayedByUserIdAsync(userId);
    }

    /// <summary>
    /// Check if user has earned a specific achievement
    /// </summary>
    public async Task<bool> HasAchievementAsync(UserId userId, AchievementId achievementId)
    {
        return await _userAchievementRepository.HasAchievementAsync(userId, achievementId);
    }

    // ==================== User Progress Queries ====================

    /// <summary>
    /// Get all achievement progress for a user
    /// </summary>
    public async Task<IEnumerable<UserAchievementProgress>> GetUserProgressAsync(UserId userId)
    {
        return await _userAchievementProgressRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Get incomplete achievement progress for a user
    /// </summary>
    public async Task<IEnumerable<UserAchievementProgress>> GetIncompleteUserProgressAsync(UserId userId)
    {
        return await _userAchievementProgressRepository.GetIncompleteByUserIdAsync(userId);
    }

    /// <summary>
    /// Get progress for a specific achievement
    /// </summary>
    public async Task<UserAchievementProgress?> GetProgressAsync(UserId userId, AchievementId achievementId)
    {
        return await _userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);
    }

    // ==================== Manual Award (Phase 1) ====================

    /// <summary>
    /// Manually award an achievement to a user (Phase 1 - no automatic triggering)
    /// </summary>
    public async Task AwardAchievementAsync(UserId userId, AchievementId achievementId)
    {
        // Check if user already has this achievement
        var alreadyHas = await _userAchievementRepository.HasAchievementAsync(userId, achievementId);
        if (alreadyHas)
        {
            throw new InvalidOperationException($"User '{userId}' already has achievement '{achievementId}'");
        }

        // Verify achievement exists
        var achievement = await _achievementRepository.GetByPublicIdAsync(achievementId);
        if (achievement == null)
        {
            throw new InvalidOperationException($"Achievement '{achievementId}' not found");
        }

        // Create user achievement
        var userAchievement = UserAchievement.Create(userId, achievementId);
        await _userAchievementRepository.AddAsync(userAchievement);

        // Clean up progress tracking if it exists
        var progress = await _userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);
        if (progress != null)
        {
            await _userAchievementProgressRepository.DeleteAsync(progress);
        }
    }

    // ==================== Progress Tracking (Manual in Phase 1) ====================

    /// <summary>
    /// Initialize progress tracking for an achievement (Phase 1 - manual)
    /// </summary>
    public async Task InitializeProgressAsync(UserId userId, AchievementId achievementId, int targetValue)
    {
        // Check if progress already exists
        var existing = await _userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);
        if (existing != null)
        {
            throw new InvalidOperationException($"Progress already exists for user '{userId}' and achievement '{achievementId}'");
        }

        // Verify achievement exists
        var achievement = await _achievementRepository.GetByPublicIdAsync(achievementId);
        if (achievement == null)
        {
            throw new InvalidOperationException($"Achievement '{achievementId}' not found");
        }

        // Create progress
        var progress = UserAchievementProgress.Create(userId, achievementId, targetValue);
        await _userAchievementProgressRepository.AddAsync(progress);
    }

    /// <summary>
    /// Update progress for an achievement (Phase 1 - manual)
    /// In Phase 2+, this will be called automatically by event handlers
    /// </summary>
    public async Task UpdateProgressAsync(UserId userId, AchievementId achievementId, int currentValue, string? progressData = null)
    {
        var progress = await _userAchievementProgressRepository.GetByUserAndAchievementAsync(userId, achievementId);
        if (progress == null)
        {
            throw new InvalidOperationException($"Progress not found for user '{userId}' and achievement '{achievementId}'");
        }

        // Update progress
        progress.UpdateProgress(currentValue, progressData);

        // Check if achievement is now complete
        if (progress.IsComplete())
        {
            // Auto-award the achievement
            var alreadyHas = await _userAchievementRepository.HasAchievementAsync(userId, achievementId);
            if (!alreadyHas)
            {
                var userAchievement = UserAchievement.Create(userId, achievementId);
                await _userAchievementRepository.AddAsync(userAchievement);
            }

            // Delete progress since achievement is earned
            await _userAchievementProgressRepository.DeleteAsync(progress);
        }
        else
        {
            // Save progress
            await _userAchievementProgressRepository.UpdateAsync(progress);
        }
    }

    // ==================== Display Management ====================

    /// <summary>
    /// Update achievement display settings (for profile showcase)
    /// </summary>
    public async Task UpdateAchievementDisplayAsync(UserAchievementId userAchievementId, bool isDisplayed, int displayOrder)
    {
        var userAchievement = await _userAchievementRepository.GetByPublicIdAsync(userAchievementId);
        if (userAchievement == null)
        {
            throw new InvalidOperationException($"UserAchievement '{userAchievementId}' not found");
        }

        userAchievement.UpdateDisplay(isDisplayed, displayOrder);
        await _userAchievementRepository.UpdateAsync(userAchievement);
    }
}
