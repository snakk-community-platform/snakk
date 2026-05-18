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
    public async Task<UserAchievementProgress?> GetByUserAndAchievementAsync(
        UserId userId,
        AchievementId achievementId,
        CancellationToken ct = default)
    {
        var projection = await context.UserAchievementProgress
            .Where(p =>
                p.UserPublicId == userId.Value
                && p.AchievementPublicId == achievementId.Value)
            .Select(p => new UserAchievementProgressProjection(
                p.UserPublicId, p.AchievementPublicId,
                p.CurrentValue, p.TargetValue, p.ProgressData, p.LastUpdated))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<UserAchievementProgress>> GetByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var projections = await context.UserAchievementProgress
            .Where(p => p.UserPublicId == userId.Value)
            .Select(p => new UserAchievementProgressProjection(
                p.UserPublicId, p.AchievementPublicId,
                p.CurrentValue, p.TargetValue, p.ProgressData, p.LastUpdated))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<IEnumerable<UserAchievementProgress>> GetIncompleteByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var projections = await context.UserAchievementProgress
            .Where(p =>
                p.UserPublicId == userId.Value
                && p.CurrentValue < p.TargetValue)
            .Select(p => new UserAchievementProgressProjection(
                p.UserPublicId, p.AchievementPublicId,
                p.CurrentValue, p.TargetValue, p.ProgressData, p.LastUpdated))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(UserAchievementProgress progress, CancellationToken ct = default)
    {
        var entity = progress.ToPersistence();

        // Resolve foreign keys from PublicIds
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value, ct);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await context.Achievements.FirstOrDefaultAsync(a => a.PublicId == progress.AchievementId.Value, ct);

        if (achievement is null)
            throw new InvalidOperationException($"Achievement with PublicId '{progress.AchievementId}' not found");

        entity.UserId = user.Id;
        entity.UserPublicId = user.PublicId;
        entity.AchievementId = achievement.Id;
        entity.AchievementPublicId = achievement.PublicId;

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserAchievementProgress progress, CancellationToken ct = default)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value, ct);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await context.Achievements
            .FirstOrDefaultAsync(a => a.PublicId == progress.AchievementId.Value, ct);

        if (achievement is null)
            throw new InvalidOperationException($"Achievement with PublicId '{progress.AchievementId}' not found");

        var entity = await context.UserAchievementProgress
            .FirstOrDefaultAsync(p =>
                p.UserId == user.Id
                && p.AchievementId == achievement.Id, ct);

        if (entity is null)
            throw new InvalidOperationException($"UserAchievementProgress for User '{progress.UserId}' and Achievement '{progress.AchievementId}' not found");

        // Update properties
        entity.CurrentValue = progress.CurrentValue;
        entity.TargetValue = progress.TargetValue;
        entity.ProgressData = progress.ProgressData;
        entity.LastUpdated = progress.LastUpdated;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(UserAchievementProgress progress, CancellationToken ct = default)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value, ct);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await context.Achievements
            .FirstOrDefaultAsync(a => a.PublicId == progress.AchievementId.Value, ct);

        if (achievement is null)
            throw new InvalidOperationException($"Achievement with PublicId '{progress.AchievementId}' not found");

        var entity = await context.UserAchievementProgress
            .FirstOrDefaultAsync(p =>
                p.UserId == user.Id
                && p.AchievementId == achievement.Id, ct);

        if (entity is null)
            throw new InvalidOperationException($"UserAchievementProgress for User '{progress.UserId}' and Achievement '{progress.AchievementId}' not found");

        await databaseRepository.DeleteAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
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
