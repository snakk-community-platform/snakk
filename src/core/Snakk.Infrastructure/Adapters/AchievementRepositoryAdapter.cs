namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

public class AchievementRepositoryAdapter(
    Infrastructure.Database.Repositories.IAchievementRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IAchievementRepository
{
    private readonly Infrastructure.Database.Repositories.IAchievementRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Achievement?> GetByIdAsync(int id)
    {
        var entity = await _databaseRepository.GetByIdAsync(id);
        return entity?.FromPersistence();
    }

    public async Task<Achievement?> GetByPublicIdAsync(AchievementId publicId)
    {
        var entity = await _databaseRepository.GetByPublicIdAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<Achievement?> GetBySlugAsync(string slug)
    {
        var entity = await _databaseRepository.GetBySlugAsync(slug);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<Achievement>> GetAllActiveAsync()
    {
        var entities = await _databaseRepository.GetAllActiveAsync();
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<Achievement>> GetByCategoryAsync(AchievementCategoryEnum category)
    {
        var entities = await _databaseRepository.GetByCategoryIdAsync((int)category);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddAsync(Achievement achievement)
    {
        var entity = achievement.ToPersistence();
        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Achievement achievement)
    {
        var entity = await _context.Achievements
            .FirstOrDefaultAsync(a => a.PublicId == achievement.PublicId.Value);

        if (entity == null)
            throw new InvalidOperationException($"Achievement with PublicId '{achievement.PublicId}' not found");

        // Update properties
        entity.Name = achievement.Name;
        entity.Description = achievement.Description;
        entity.IconUrl = achievement.IconUrl;
        entity.IsActive = achievement.IsActive;
        entity.DisplayOrder = achievement.DisplayOrder;
        entity.UpdatedAt = achievement.UpdatedAt;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }
}
