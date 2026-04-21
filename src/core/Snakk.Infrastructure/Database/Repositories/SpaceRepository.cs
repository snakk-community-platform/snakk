namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class SpaceRepository(SnakkDbContext context)
    : GenericDatabaseRepository<SpaceDatabaseEntity>(context), ISpaceRepository
{
    public record SpaceListDto(
        string PublicId,
        string Name,
        string Slug,
        string? Description,
        bool AllowAnonymousReading,
        bool RequireEmailConfirmation,
        DateTime CreatedAt,
        string HubPublicId,
        string HubName);

    public override async Task<SpaceDatabaseEntity?> GetByIdAsync(int id) =>
        await _dbSet.FirstOrDefaultAsync(s => s.Id == id);

    public async Task<SpaceDatabaseEntity?> GetForUpdateAsync(string publicId) => await _dbSet
        .AsTracking()
        .Include(s => s.Hub)
        .FirstOrDefaultAsync(s => s.PublicId == publicId);

    public override async Task<IEnumerable<SpaceDatabaseEntity>> GetAllAsync() =>
        await _dbSet.AsNoTracking()
            .Include(s => s.Hub)
            .Take(1000)
            .ToListAsync();

    public async Task<SpaceDetailDto?> GetForDisplayAsync(string publicId) => await _dbSet
        .Where(s => s.PublicId == publicId)
        .Select(s => new SpaceDetailDto(
            s.PublicId,
            s.Name,
            s.Slug,
            s.Description,
            s.AllowAnonymousReading,
            s.RequireEmailConfirmation,
            s.CreatedAt,
            s.Hub.PublicId,
            s.Hub.Name))
        .FirstOrDefaultAsync();

    public async Task<SpaceDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet.FirstOrDefaultAsync(s => s.PublicId == publicId);

    public async Task<SpaceDatabaseEntity?> GetBySlugAsync(string slug, string hubSlug) =>
        await _dbSet.FirstOrDefaultAsync(s => s.Slug == slug && s.Hub.Slug == hubSlug);

    public async Task<PagedResult<SpaceListDto>> GetFilteredForDisplayAsync(
        string hubPublicId,
        int offset,
        int pageSize)
    {
        var items = await _dbSet
            .Where(s => s.Hub.PublicId == hubPublicId)
            .OrderBy(s => s.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(s => new SpaceListDto(
                s.PublicId,
                s.Name,
                s.Slug,
                s.Description,
                s.AllowAnonymousReading,
                s.RequireEmailConfirmation,
                s.CreatedAt,
                s.Hub.PublicId,
                s.Hub.Name))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        return new PagedResult<SpaceListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }
}
