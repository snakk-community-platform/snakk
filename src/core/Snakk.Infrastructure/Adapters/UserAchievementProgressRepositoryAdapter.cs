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
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user == null)
            return null;

        var achievement = await _context.Achievements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == achievementId.Value);

        if (achievement == null)
            return null;

        var entity = await _databaseRepository.GetByUserAndAchievementAsync(user.Id, achievement.Id);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<UserAchievementProgress>> GetByUserIdAsync(UserId userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user == null)
            return [];

        var entities = await _databaseRepository.GetByUserIdAsync(user.Id);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<UserAchievementProgress>> GetIncompleteByUserIdAsync(UserId userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user == null)
            return [];

        var entities = await _databaseRepository.GetIncompleteByUserIdAsync(user.Id);
        return entities.Select(e => e.FromPersistence());
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
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value);

        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await _context.Achievements
            .AsNoTracking()
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
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == progress.UserId.Value);

        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{progress.UserId}' not found");

        var achievement = await _context.Achievements
            .AsNoTracking()
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
}
