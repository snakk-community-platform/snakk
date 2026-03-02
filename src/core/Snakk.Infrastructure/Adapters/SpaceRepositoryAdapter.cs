namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class SpaceRepositoryAdapter(
    Infrastructure.Database.Repositories.ISpaceRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.ISpaceRepository
{
    public async Task<Space?> GetByIdAsync(int id)
    {
        var projection = await context.Spaces
            .Where(s => s.Id == id)
            .Select(s => new SpaceProjection(
                s.PublicId, s.Hub.PublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Space?> GetByPublicIdAsync(SpaceId publicId)
    {
        var projection = await context.Spaces
            .Where(s => s.PublicId == publicId.Value)
            .Select(s => new SpaceProjection(
                s.PublicId, s.Hub.PublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Space?> GetBySlugAsync(string slug)
    {
        var projection = await context.Spaces
            .Where(s => s.Slug == slug)
            .Select(s => new SpaceProjection(
                s.PublicId, s.Hub.PublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Space>> GetFilteredForDisplayAsync(
        HubId hubId,
        int offset,
        int pageSize)
    {
        var result = await databaseRepository.GetFilteredForDisplayAsync(hubId.Value, offset, pageSize);

        return new PagedResult<Space>
        {
            Items = result.Items
                .Select(dto => Space.RehydrateForList(
                    SpaceId.From(dto.PublicId),
                    HubId.From(dto.HubPublicId),
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

    public async Task<IEnumerable<Space>> GetAllAsync()
    {
        var projections = await context.Spaces
            .Select(s => new SpaceProjection(
                s.PublicId, s.Hub.PublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(Space space)
    {
        var entity = space.ToPersistence();

        var hub = await context.Hubs.FirstOrDefaultAsync(h => h.PublicId == space.HubId.Value);

        if (hub is null)
            throw new InvalidOperationException($"Hub with PublicId '{space.HubId}' not found");

        entity.HubId = hub.Id;

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Space space)
    {
        var entity = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == space.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Space with PublicId '{space.PublicId}' not found");

        entity.Name = space.Name;
        entity.Slug = space.Slug;
        entity.Description = space.Description;
        entity.AllowAnonymousReading = space.AllowAnonymousReading;
        entity.RequireEmailConfirmation = space.RequireEmailConfirmation;
        entity.LastModifiedAt = space.LastModifiedAt;

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    private record SpaceProjection(
        string PublicId,
        string HubPublicId,
        string Name,
        string Slug,
        string? Description,
        bool AllowAnonymousReading,
        bool RequireEmailConfirmation,
        DateTime CreatedAt,
        DateTime? LastModifiedAt)
    {
        public Space ToDomain() => Space.Rehydrate(
            SpaceId.From(PublicId),
            HubId.From(HubPublicId),
            Name, Slug, Description,
            AllowAnonymousReading, RequireEmailConfirmation,
            CreatedAt, LastModifiedAt, discussions: []);
    }
}
