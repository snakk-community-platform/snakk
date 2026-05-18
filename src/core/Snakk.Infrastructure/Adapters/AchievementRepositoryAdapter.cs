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
    public async Task<Achievement?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var projection = await context.Achievements
            .Where(a => a.Id == id)
            .Select(a => new AchievementProjection(a))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Achievement?> GetByPublicIdAsync(AchievementId publicId, CancellationToken ct = default)
    {
        var projection = await context.Achievements
            .Where(a => a.PublicId == publicId.Value)
            .Select(a => new AchievementProjection(a))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<Achievement>> GetByIdsAsync(IEnumerable<AchievementId> ids, CancellationToken ct = default)
    {
        var publicIds = ids.Select(id => id.Value).ToList();

        if (publicIds.Count == 0)
            return [];

        var projections = await context.Achievements
            .Where(a => publicIds.Contains(a.PublicId))
            .Select(a => new AchievementProjection(a))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<Achievement?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var projection = await context.Achievements
            .Where(a => a.Slug == slug)
            .Select(a => new AchievementProjection(a))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<Achievement>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var projections = await context.Achievements
            .Where(a => a.IsActive)
            .Select(a => new AchievementProjection(a))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<IEnumerable<Achievement>> GetByCategoryAsync(AchievementCategoryEnum category, CancellationToken ct = default)
    {
        var projections = await context.Achievements
            .Where(a => a.CategoryId == (int)category)
            .Select(a => new AchievementProjection(a))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(Achievement achievement, CancellationToken ct = default)
    {
        var entity = achievement.ToPersistence();
        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Achievement achievement, CancellationToken ct = default)
    {
        var entity = await context.Achievements
            .FirstOrDefaultAsync(a => a.PublicId == achievement.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Achievement with PublicId '{achievement.PublicId}' not found");

        // Update properties
        entity.Name = achievement.Name;
        entity.Description = achievement.Description;
        entity.IconUrl = achievement.IconUrl;
        entity.IsActive = achievement.IsActive;
        entity.DisplayOrder = achievement.DisplayOrder;
        entity.UpdatedAt = achievement.UpdatedAt;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    private record AchievementProjection
    {
        public string PublicId { get; init; }
        public string Slug { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public string? IconUrl { get; init; }
        public int CategoryId { get; init; }
        public int TierLevel { get; init; }
        public int Points { get; init; }
        public bool IsSecret { get; init; }
        public bool IsActive { get; init; }
        public int RequirementTypeId { get; init; }
        public string RequirementConfig { get; init; }
        public int DisplayOrder { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public AchievementProjection(Database.Entities.AchievementDatabaseEntity a)
        {
            PublicId = a.PublicId;
            Slug = a.Slug;
            Name = a.Name;
            Description = a.Description;
            IconUrl = a.IconUrl;
            CategoryId = a.CategoryId;
            TierLevel = a.TierLevel;
            Points = a.Points;
            IsSecret = a.IsSecret;
            IsActive = a.IsActive;
            RequirementTypeId = a.RequirementTypeId;
            RequirementConfig = a.RequirementConfig;
            DisplayOrder = a.DisplayOrder;
            CreatedAt = a.CreatedAt;
            UpdatedAt = a.UpdatedAt;
        }

        public Achievement ToDomain() => Achievement.Rehydrate(
            AchievementId.From(PublicId),
            Slug, Name, Description, IconUrl,
            (AchievementCategoryEnum)CategoryId,
            (AchievementTierEnum)TierLevel,
            Points, IsSecret, IsActive,
            (AchievementRequirementTypeEnum)RequirementTypeId,
            RequirementConfig, DisplayOrder,
            CreatedAt, UpdatedAt);
    }
}
