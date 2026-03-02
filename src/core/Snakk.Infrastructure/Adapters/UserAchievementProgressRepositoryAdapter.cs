namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;

public class UserAchievementProgressRepositoryAdapter(
    Infrastructure.Database.Repositories.IUserAchievementProgressRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IUserAchievementProgressRepository
{
    private readonly Infrastructure.Database.Repositories.IUserAchievementProgressRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<UserAchievementProgress?> GetByUserAndAchievementAsync(UserId userId, AchievementId achievementId)
    {
        var projection = await _context.UserAchievementProgress
            .Where(p => p.User.PublicId == userId.Value && p.Achievement.PublicId == achievementId.Value)
            .Select(p => new UserAchievementProgressProjection(
                p.User.PublicId, p.Achievement.PublicId,
                p.CurrentValue, p.TargetValue, p.ProgressData, p.LastUpdated))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<UserAchievementProgress>> GetByUserIdAsync(UserId userId)
    {
        var projections = await _context.UserAchievementProgress
            .Where(p => p.User.PublicId == userId.Value)
            .Select(p => new UserAchievementProgressProjection(
                p.User.PublicId, p.Achievement.PublicId,
                p.CurrentValue, p.TargetValue, p.ProgressData, p.LastUpdated))
            .ToListAsync();
        return projections.Select(p => p.ToDomain());
    }

    public async Task<IEnumerable<UserAchievementProgress>> GetIncompleteByUserIdAsync(UserId userId)
    {
        var projections = await _context.UserAchievementProgress
            .Where(p => p.User.PublicId == userId.Value && p.CurrentValue < p.TargetValue)
            .Select(p => new UserAchievementProgressProjection(
                p.User.PublicId, p.Achievement.PublicId,
                p.CurrentValue, p.TargetValue, p.ProgressData, p.LastUpdated))
            .ToListAsync();
        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(UserAchievementProgress progress)
    {
        var entity = progress.ToPersistence();

        // Resolve foreign keys from PublicIds
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value);
        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await _context.Achievements.FirstOrDefaultAsync(a => a.PublicId == progress.AchievementId.Value);
        if (achievement == null)
            throw new InvalidOperationException($"Achievement with PublicId '{progress.AchievementId}' not found");

        entity.UserId = user.Id;
        entity.AchievementId = achievement.Id;

        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserAchievementProgress progress)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value);

        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await _context.Achievements
            .FirstOrDefaultAsync(a => a.PublicId == progress.AchievementId.Value);

        if (achievement == null)
            throw new InvalidOperationException($"Achievement with PublicId '{progress.AchievementId}' not found");

        var entity = await _context.UserAchievementProgress
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.AchievementId == achievement.Id);

        if (entity == null)
            throw new InvalidOperationException($"UserAchievementProgress for User '{progress.UserId}' and Achievement '{progress.AchievementId}' not found");

        // Update properties
        entity.CurrentValue = progress.CurrentValue;
        entity.TargetValue = progress.TargetValue;
        entity.ProgressData = progress.ProgressData;
        entity.LastUpdated = progress.LastUpdated;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserAchievementProgress progress)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value);

        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await _context.Achievements
            .FirstOrDefaultAsync(a => a.PublicId == progress.AchievementId.Value);

        if (achievement == null)
            throw new InvalidOperationException($"Achievement with PublicId '{progress.AchievementId}' not found");

        var entity = await _context.UserAchievementProgress
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.AchievementId == achievement.Id);

        if (entity == null)
            throw new InvalidOperationException($"UserAchievementProgress for User '{progress.UserId}' and Achievement '{progress.AchievementId}' not found");

        await _databaseRepository.DeleteAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    private record UserAchievementProgressProjection(
        string UserPublicId,
        string AchievementPublicId,
        int CurrentValue,
        int TargetValue,
        string? ProgressData,
        DateTime LastUpdated)
    {
        public UserAchievementProgress ToDomain() => UserAchievementProgress.Rehydrate(
            UserId.From(UserPublicId),
            AchievementId.From(AchievementPublicId),
            CurrentValue, TargetValue, ProgressData, LastUpdated);
    }
}
