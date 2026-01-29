namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class HubRepositoryAdapter(
    Infrastructure.Database.Repositories.IHubRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IHubRepository
{
    private readonly Infrastructure.Database.Repositories.IHubRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Hub?> GetByIdAsync(int id)
    {
        var entity = await _databaseRepository.GetByIdAsync(id);
        return entity?.FromPersistence();
    }

    public async Task<Hub?> GetByPublicIdAsync(HubId publicId)
    {
        var entity = await _databaseRepository.GetForUpdateAsync(publicId.Value);
        return entity?.FromPersistence();
    }

    public async Task<Hub?> GetBySlugAsync(string slug)
    {
        var entity = await _databaseRepository.GetBySlugAsync(slug);
        return entity?.FromPersistence();
    }

    public async Task<IEnumerable<Hub>> GetAllAsync()
    {
        var entities = await _databaseRepository.GetAllAsync();
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<PagedResult<Hub>> GetFilteredForDisplayAsync(int offset, int pageSize)
    {
        var result = await _databaseRepository.GetFilteredForDisplayAsync(offset, pageSize);
        return new PagedResult<Hub>
        {
            Items = result.Items.Select(dto => Hub.RehydrateForList(
                HubId.From(dto.PublicId),
                CommunityId.From(dto.CommunityPublicId),
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

    public async Task<PagedResult<Hub>> GetByCommunityAsync(CommunityId communityId, int offset, int pageSize)
    {
        // First get the community's database ID
        var communityDbId = await _databaseRepository.GetCommunityDbIdAsync(communityId.Value);
        if (communityDbId == null)
        {
            return new PagedResult<Hub>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };
        }

        var result = await _databaseRepository.GetByCommunityAsync(communityDbId.Value, offset, pageSize);
        return new PagedResult<Hub>
        {
            Items = result.Items.Select(dto => Hub.RehydrateForList(
                HubId.From(dto.PublicId),
                CommunityId.From(dto.CommunityPublicId),
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

    public async Task AddAsync(Hub hub)
    {
        // Resolve CommunityId to database ID
        var communityDbId = await _databaseRepository.GetCommunityDbIdAsync(hub.CommunityId.Value);
        if (communityDbId == null)
            throw new InvalidOperationException($"Community with PublicId '{hub.CommunityId}' not found");

        var entity = hub.ToPersistence(communityDbId.Value);
        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Hub hub)
    {
        var entity = await _context.Hubs
            .Include(h => h.Community)
            .FirstOrDefaultAsync(h => h.PublicId == hub.PublicId.Value);
        if (entity == null)
            throw new InvalidOperationException($"Hub with PublicId '{hub.PublicId}' not found");

        entity.Name = hub.Name;
        entity.Slug = hub.Slug;
        entity.Description = hub.Description;
        entity.AllowAnonymousReading = hub.AllowAnonymousReading;
        entity.RequireEmailConfirmation = hub.RequireEmailConfirmation;
        entity.LastModifiedAt = hub.LastModifiedAt;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }
}
