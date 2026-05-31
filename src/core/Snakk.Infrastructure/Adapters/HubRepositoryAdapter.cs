namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class HubRepositoryAdapter(
    Infrastructure.Database.Repositories.IHubRepository databaseRepository,
    SnakkDbContext context,
    HybridCache cache) : Domain.Repositories.IHubRepository
{
    public async Task<Hub?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var projection = await context.Hubs
            .Where(h => h.Id == id)
            .Select(h => new HubProjection(
                h.PublicId, h.CommunityPublicId, h.Name, h.Slug, h.Description,
                h.AllowAnonymousReading, h.RequireEmailConfirmation,
                h.CreatedAt, h.LastModifiedAt,
                h.AvatarFileName, h.AvatarThumbnailFileName, h.AvatarMicroFileName, h.AvatarRevision))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Hub?> GetByPublicIdAsync(HubId publicId, CancellationToken ct = default)
    {
        var projection = await context.Hubs
            .Where(h => h.PublicId == publicId.Value)
            .Select(h => new HubProjection(
                h.PublicId, h.CommunityPublicId, h.Name, h.Slug, h.Description,
                h.AllowAnonymousReading, h.RequireEmailConfirmation,
                h.CreatedAt, h.LastModifiedAt,
                h.AvatarFileName, h.AvatarThumbnailFileName, h.AvatarMicroFileName, h.AvatarRevision))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Hub?> GetBySlugAsync(string slug, string communitySlug, CancellationToken ct = default)
    {
        var projection = await context.Hubs
            .Where(h => h.Slug == slug && h.Community.Slug == communitySlug)
            .Select(h => new HubProjection(
                h.PublicId, h.CommunityPublicId, h.Name, h.Slug, h.Description,
                h.AllowAnonymousReading, h.RequireEmailConfirmation,
                h.CreatedAt, h.LastModifiedAt,
                h.AvatarFileName, h.AvatarThumbnailFileName, h.AvatarMicroFileName, h.AvatarRevision))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<Hub>> GetAllAsync(CancellationToken ct = default)
    {
        var projections = await context.Hubs
            .Select(h => new HubProjection(
                h.PublicId, h.CommunityPublicId, h.Name, h.Slug, h.Description,
                h.AllowAnonymousReading, h.RequireEmailConfirmation,
                h.CreatedAt, h.LastModifiedAt,
                h.AvatarFileName, h.AvatarThumbnailFileName, h.AvatarMicroFileName, h.AvatarRevision))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<PagedResult<Hub>> GetFilteredForDisplayAsync(int offset, int pageSize, CancellationToken ct = default)
    {
        var result = await databaseRepository.GetFilteredForDisplayAsync(offset, pageSize, ct);

        return new PagedResult<Hub>
        {
            Items = result.Items
                .Select(dto => Hub.RehydrateForList(
                    HubId.From(dto.PublicId),
                    CommunityId.From(dto.CommunityPublicId),
                    dto.Name,
                    dto.Slug,
                    dto.Description,
                    dto.AllowAnonymousReading,
                    dto.RequireEmailConfirmation,
                    dto.CreatedAt))
                .ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<PagedResult<Hub>> GetByCommunityAsync(
        CommunityId communityId,
        int offset,
        int pageSize,
        string? userId = null,
        CancellationToken ct = default)
    {
        // First get the community's database ID
        var communityDbId = await databaseRepository.GetCommunityDbIdAsync(communityId.Value, ct);

        if (communityDbId is null)
        {
            return new PagedResult<Hub>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };
        }

        var result = await databaseRepository.GetByCommunityAsync(communityDbId.Value, offset, pageSize, userId, ct);

        return new PagedResult<Hub>
        {
            Items = result.Items
                .Select(dto => Hub.RehydrateForList(
                    HubId.From(dto.PublicId),
                    CommunityId.From(dto.CommunityPublicId),
                    dto.Name,
                    dto.Slug,
                    dto.Description,
                    dto.AllowAnonymousReading,
                    dto.RequireEmailConfirmation,
                    dto.CreatedAt))
                .ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task AddAsync(Hub hub, CancellationToken ct = default)
    {
        // Resolve CommunityId to database ID
        var communityDbId = await databaseRepository.GetCommunityDbIdAsync(hub.CommunityId.Value, ct);

        if (communityDbId is null)
            throw new InvalidOperationException($"Community with PublicId '{hub.CommunityId}' not found");

        var entity = hub.ToPersistence(communityDbId.Value);
        entity.CommunityPublicId = hub.CommunityId.Value;
        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Hub hub, CancellationToken ct = default)
    {
        var entity = await context.Hubs
            .FirstOrDefaultAsync(h => h.PublicId == hub.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Hub with PublicId '{hub.PublicId}' not found");

        var nameChanged = entity.Name != hub.Name;
        var slugChanged = entity.Slug != hub.Slug;

        entity.Name = hub.Name;
        entity.Slug = hub.Slug;
        entity.Description = hub.Description;
        entity.AllowAnonymousReading = hub.AllowAnonymousReading;
        entity.RequireEmailConfirmation = hub.RequireEmailConfirmation;
        entity.LastModifiedAt = hub.LastModifiedAt;
        entity.AvatarFileName = hub.AvatarFileName;
        entity.AvatarThumbnailFileName = hub.AvatarThumbnailFileName;
        entity.AvatarMicroFileName = hub.AvatarMicroFileName;
        entity.AvatarRevision = hub.AvatarRevision;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);

        if (nameChanged || slugChanged)
        {
            var affectedSpaceIds = await context.Spaces
                .Where(s => s.HubId == entity.Id)
                .Select(s => s.Id)
                .ToListAsync(ct);

            await context.Spaces
                .Where(s => s.HubId == entity.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(sp => sp.HubName, entity.Name)
                    .SetProperty(sp => sp.HubSlug, entity.Slug), ct);

            foreach (var id in affectedSpaceIds)
                await cache.RemoveAsync($"space-display:{id}", ct);
        }
    }

    private record HubProjection(
        string PublicId,
        string CommunityPublicId,
        string Name,
        string Slug,
        string? Description,
        bool AllowAnonymousReading,
        bool RequireEmailConfirmation,
        DateTime CreatedAt,
        DateTime? LastModifiedAt,
        string? AvatarFileName,
        string? AvatarThumbnailFileName,
        string? AvatarMicroFileName,
        int AvatarRevision)
    {
        public Hub ToDomain() => Hub.Rehydrate(
            HubId.From(PublicId),
            CommunityId.From(CommunityPublicId),
            Name, Slug, Description,
            AllowAnonymousReading, RequireEmailConfirmation,
            CreatedAt, LastModifiedAt, spaces: [],
            avatarFileName: AvatarFileName,
            avatarThumbnailFileName: AvatarThumbnailFileName,
            avatarMicroFileName: AvatarMicroFileName,
            avatarRevision: AvatarRevision);
    }
}
