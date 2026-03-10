namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

public class AnnouncementRepositoryAdapter(
    Infrastructure.Database.Repositories.IAnnouncementDatabaseRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IAnnouncementRepository
{
    public async Task<Announcement?> GetByPublicIdAsync(AnnouncementId publicId)
    {
        var entity = await databaseRepository.GetByPublicIdAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<Announcement>> GetByScopeAsync(
        AnnouncementScopeEnum scope,
        string scopeEntityId)
    {
        var entities = await context.Announcements
            .Include(a => a.CreatedByUser)
            .Where(a =>
                a.ScopeId == (int)scope
                && a.ScopeEntityId == scopeEntityId)
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<Announcement>> GetActiveForCommunityAsync(CommunityId communityId)
    {
        var now = DateTime.UtcNow;

        var entities = await context.Announcements
            .Include(a => a.CreatedByUser)
            .Where(a =>
                a.ScopeId == (int)AnnouncementScopeEnum.Community
                && a.ScopeEntityId == communityId.Value
                && (a.VisibleFrom == null || a.VisibleFrom <= now)
                && (a.VisibleUntil == null || a.VisibleUntil >= now))
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<Announcement>> GetActiveForHubAsync(HubId hubId)
    {
        var now = DateTime.UtcNow;

        // Resolve hub and its parent community
        var hub = await context.Hubs
            .Where(h => h.PublicId == hubId.Value)
            .Select(h => new { h.PublicId, CommunityPublicId = h.Community.PublicId })
            .FirstOrDefaultAsync();

        if (hub is null)
            return [];

        var scopeFilters = new List<(int ScopeId, string EntityId)>
        {
            ((int)AnnouncementScopeEnum.Hub, hub.PublicId),
            ((int)AnnouncementScopeEnum.Community, hub.CommunityPublicId)
        };

        var entities = await context.Announcements
            .Include(a => a.CreatedByUser)
            .Where(a =>
                scopeFilters.Any(f => a.ScopeId == f.ScopeId && a.ScopeEntityId == f.EntityId)
                && (a.VisibleFrom == null || a.VisibleFrom <= now)
                && (a.VisibleUntil == null || a.VisibleUntil >= now))
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.FromPersistence());
    }

    public async Task<IEnumerable<Announcement>> GetActiveForSpaceAsync(SpaceId spaceId)
    {
        var now = DateTime.UtcNow;

        // Resolve space, its parent hub, and grandparent community
        var space = await context.Spaces
            .Where(s => s.PublicId == spaceId.Value)
            .Select(s => new
            {
                s.PublicId,
                HubPublicId = s.Hub.PublicId,
                CommunityPublicId = s.Hub.Community.PublicId
            })
            .FirstOrDefaultAsync();

        if (space is null)
            return [];

        var scopeFilters = new List<(int ScopeId, string EntityId)>
        {
            ((int)AnnouncementScopeEnum.Space, space.PublicId),
            ((int)AnnouncementScopeEnum.Hub, space.HubPublicId),
            ((int)AnnouncementScopeEnum.Community, space.CommunityPublicId)
        };

        var entities = await context.Announcements
            .Include(a => a.CreatedByUser)
            .Where(a =>
                scopeFilters.Any(f => a.ScopeId == f.ScopeId && a.ScopeEntityId == f.EntityId)
                && (a.VisibleFrom == null || a.VisibleFrom <= now)
                && (a.VisibleUntil == null || a.VisibleUntil >= now))
            .OrderBy(a => a.SortOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddAsync(Announcement announcement)
    {
        var entity = announcement.ToPersistence();

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == announcement.CreatedByUserId.Value);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{announcement.CreatedByUserId}' not found");

        entity.CreatedByUserId = user.Id;

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Announcement announcement)
    {
        var entity = await context.Announcements.FirstOrDefaultAsync(a => a.PublicId == announcement.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Announcement with PublicId '{announcement.PublicId}' not found");

        entity.Title = announcement.Title;
        entity.Content = announcement.Content;
        entity.RenderedContent = announcement.RenderedContent;
        entity.TypeId = (int)announcement.Type;
        entity.VisibleFrom = announcement.VisibleFrom;
        entity.VisibleUntil = announcement.VisibleUntil;
        entity.IsDismissible = announcement.IsDismissible;
        entity.SortOrder = announcement.SortOrder;
        entity.LastModifiedAt = announcement.LastModifiedAt;

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(AnnouncementId publicId)
    {
        var entity = await context.Announcements.FirstOrDefaultAsync(a => a.PublicId == publicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Announcement with PublicId '{publicId}' not found");

        context.Announcements.Remove(entity);
        await context.SaveChangesAsync();
    }
}
