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
    private readonly Infrastructure.Database.Repositories.ISpaceRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Space?> GetByIdAsync(int id)
    {
        var entity = await _databaseRepository.GetByIdAsync(id);
        return entity?.FromPersistence();
    }

    public async Task<Space?> GetByPublicIdAsync(SpaceId publicId)
    {
        var entity = await _databaseRepository.GetForUpdateAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<Space?> GetBySlugAsync(string slug)
    {
        var entity = await _databaseRepository.GetBySlugAsync(slug);
        return entity?.FromPersistence();
    }

    public async Task<PagedResult<Space>> GetFilteredForDisplayAsync(HubId hubId, int offset, int pageSize)
    {
        var result = await _databaseRepository.GetFilteredForDisplayAsync(hubId.Value, offset, pageSize);
        return new PagedResult<Space>
        {
            Items = result.Items.Select(dto => Space.RehydrateForList(
                SpaceId.From(dto.PublicId),
                HubId.From(dto.HubPublicId),
                dto.Name,
                dto.Slug,
                dto.Description,
                dto.AllowAnonymousReading,
                dto.RequireEmailConfirmation,
                dto.CreatedAt)).ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<IEnumerable<Space>> GetAllAsync()
    {
        var entities = await _databaseRepository.GetAllAsync();
        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddAsync(Space space)
    {
        var entity = space.ToPersistence();

        var hub = await _context.Hubs.FirstOrDefaultAsync(h => h.PublicId == space.HubId.Value);
        if (hub == null)
            throw new InvalidOperationException($"Hub with PublicId '{space.HubId}' not found");

        entity.HubId = hub.Id;

        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Space space)
    {
        var entity = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == space.PublicId.Value);
        if (entity == null)
            throw new InvalidOperationException($"Space with PublicId '{space.PublicId}' not found");

        entity.Name = space.Name;
        entity.Slug = space.Slug;
        entity.Description = space.Description;
        entity.AllowAnonymousReading = space.AllowAnonymousReading;
        entity.RequireEmailConfirmation = space.RequireEmailConfirmation;
        entity.LastModifiedAt = space.LastModifiedAt;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }
}
