namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;

public class UserAchievementRepositoryAdapter(
    Infrastructure.Database.Repositories.IUserAchievementRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IUserAchievementRepository
{
    private readonly Infrastructure.Database.Repositories.IUserAchievementRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<UserAchievement?> GetByIdAsync(int id)
    {
        var entity = await _databaseRepository.GetByIdAsync(id);
        return entity?.FromPersistence();
    }

    public async Task<UserAchievement?> GetByPublicIdAsync(UserAchievementId publicId)
    {
        var entity = await _databaseRepository.GetByPublicIdAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<UserAchievement>> GetByUserIdAsync(UserId userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user == null)
            return [];

        var entities = await _databaseRepository.GetByUserIdAsync(user.Id);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<UserAchievement>> GetDisplayedByUserIdAsync(UserId userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user == null)
            return [];

        var entities = await _databaseRepository.GetDisplayedByUserIdAsync(user.Id);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<bool> HasAchievementAsync(UserId userId, AchievementId achievementId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user == null)
            return false;

        var achievement = await _context.Achievements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == achievementId.Value);

        if (achievement == null)
            return false;

        return await _databaseRepository.HasAchievementAsync(user.Id, achievement.Id);
    }

    public async Task AddAsync(UserAchievement userAchievement)
    {
        var entity = userAchievement.ToPersistence();

        // Resolve foreign keys from PublicIds
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userAchievement.UserId.Value);
        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{userAchievement.UserId}' not found");

        var achievement = await _context.Achievements.FirstOrDefaultAsync(a => a.PublicId == userAchievement.AchievementId.Value);
        if (achievement == null)
            throw new InvalidOperationException($"Achievement with PublicId '{userAchievement.AchievementId}' not found");

        entity.UserId = user.Id;
        entity.AchievementId = achievement.Id;

        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserAchievement userAchievement)
    {
        var entity = await _context.UserAchievements
            .FirstOrDefaultAsync(ua => ua.PublicId == userAchievement.PublicId.Value);

        if (entity == null)
            throw new InvalidOperationException($"UserAchievement with PublicId '{userAchievement.PublicId}' not found");

        // Update properties
        entity.IsDisplayed = userAchievement.IsDisplayed;
        entity.DisplayOrder = userAchievement.DisplayOrder;
        entity.NotificationSent = userAchievement.NotificationSent;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }
}
