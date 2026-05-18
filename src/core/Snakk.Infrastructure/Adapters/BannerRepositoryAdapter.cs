namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

public class BannerRepositoryAdapter(
    Infrastructure.Database.Repositories.IBannerDatabaseRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IBannerRepository
{
    public async Task<Banner?> GetByPublicIdAsync(BannerId publicId, CancellationToken ct = default)
    {
        var entity = await databaseRepository.GetByPublicIdAsync(publicId.Value, ct);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<Banner>> GetByScopeAsync(
        BannerScopeEnum scope,
        string scopeEntityId,
        CancellationToken ct = default)
    {
        var entities = await context.Banners
            .Where(a =>
                a.ScopeId == (int)scope
                && a.ScopeEntityId == scopeEntityId)
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<Banner>> GetActiveForCommunityAsync(CommunityId communityId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var entities = await context.Banners
            .Where(a =>
                a.ScopeId == (int)BannerScopeEnum.Community
                && a.ScopeEntityId == communityId.Value
                && (a.VisibleFrom == null || a.VisibleFrom <= now)
                && (a.VisibleUntil == null || a.VisibleUntil >= now))
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<Banner>> GetActiveForHubAsync(HubId hubId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Resolve hub and its parent community
        var hub = await context.Hubs
            .Where(h => h.PublicId == hubId.Value)
            .Select(h => new { h.PublicId, h.CommunityPublicId })
            .FirstOrDefaultAsync(ct);

        if (hub is null)
            return [];

        var hubScope = (int)BannerScopeEnum.Hub;
        var communityScope = (int)BannerScopeEnum.Community;

        var entities = await context.Banners
            .Where(a =>
                ((a.ScopeId == hubScope && a.ScopeEntityId == hub.PublicId)
                || (a.ScopeId == communityScope && a.ScopeEntityId == hub.CommunityPublicId))
                && (a.VisibleFrom == null || a.VisibleFrom <= now)
                && (a.VisibleUntil == null || a.VisibleUntil >= now))
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<Banner>> GetActiveForSpaceAsync(SpaceId spaceId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Resolve space, its parent hub, and grandparent community
        var space = await context.Spaces
            .Where(s => s.PublicId == spaceId.Value)
            .Select(s => new
            {
                s.PublicId,
                s.HubPublicId,
                s.CommunityPublicId
            })
            .FirstOrDefaultAsync(ct);

        if (space is null)
            return [];

        var spaceScope = (int)BannerScopeEnum.Space;
        var hubScope = (int)BannerScopeEnum.Hub;
        var communityScope = (int)BannerScopeEnum.Community;

        var entities = await context.Banners
            .Where(a =>
                ((a.ScopeId == spaceScope && a.ScopeEntityId == space.PublicId)
                || (a.ScopeId == hubScope && a.ScopeEntityId == space.HubPublicId)
                || (a.ScopeId == communityScope && a.ScopeEntityId == space.CommunityPublicId))
                && (a.VisibleFrom == null || a.VisibleFrom <= now)
                && (a.VisibleUntil == null || a.VisibleUntil >= now))
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddAsync(Banner banner, CancellationToken ct = default)
    {
        var entity = banner.ToPersistence();

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == banner.CreatedByUserId.Value, ct);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{banner.CreatedByUserId}' not found");

        entity.CreatedByUserId = user.Id;
        entity.CreatedByUserPublicId = banner.CreatedByUserId.Value;

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Banner banner, CancellationToken ct = default)
    {
        var entity = await context.Banners.FirstOrDefaultAsync(a => a.PublicId == banner.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Banner with PublicId '{banner.PublicId}' not found");

        entity.Title = banner.Title;
        entity.Content = banner.Content;
        entity.RenderedContent = banner.RenderedContent;
        entity.TypeId = (int)banner.Type;
        entity.VisibleFrom = banner.VisibleFrom;
        entity.VisibleUntil = banner.VisibleUntil;
        entity.IsDismissible = banner.IsDismissible;
        entity.SortOrder = banner.SortOrder;
        entity.LastModifiedAt = banner.LastModifiedAt;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(BannerId publicId, CancellationToken ct = default)
    {
        var entity = await context.Banners.FirstOrDefaultAsync(a => a.PublicId == publicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Banner with PublicId '{publicId}' not found");

        context.Banners.Remove(entity);
        await context.SaveChangesAsync(ct);
    }
}
