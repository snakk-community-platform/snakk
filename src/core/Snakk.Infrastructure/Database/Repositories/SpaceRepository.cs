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

    public override async Task<SpaceDatabaseEntity?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<SpaceDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .AsTracking()
        .Include(s => s.Hub)
        .FirstOrDefaultAsync(s => s.PublicId == publicId, ct);

    public override async Task<IEnumerable<SpaceDatabaseEntity>> GetAllAsync(CancellationToken ct = default) =>
        await _dbSet.AsNoTracking()
            .Include(s => s.Hub)
            .Take(1000)
            .ToListAsync(ct);

    public async Task<SpaceDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Where(s => s.PublicId == publicId)
        .Select(s => new SpaceDetailDto(
            s.PublicId,
            s.Name,
            s.Slug,
            s.Description,
            s.AllowAnonymousReading,
            s.RequireEmailConfirmation,
            s.CreatedAt,
            s.HubPublicId,
            s.HubName))
        .FirstOrDefaultAsync(ct);

    public async Task<SpaceDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(s => s.PublicId == publicId, ct);

    public async Task<SpaceDatabaseEntity?> GetBySlugAsync(string slug, string hubSlug, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(s => s.Slug == slug && s.HubSlug == hubSlug, ct);

    public async Task<PagedResult<SpaceListDto>> GetFilteredForDisplayAsync(
        string hubPublicId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var hubDbId = await context.Hubs.Where(h => h.PublicId == hubPublicId).Select(h => h.Id).FirstOrDefaultAsync(ct);
        var items = await _dbSet
            .Where(s => s.HubId == hubDbId)
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
                s.HubPublicId,
                s.HubName))
            .ToListAsync(ct);

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
