namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class HubRepository(SnakkDbContext context)
    : GenericDatabaseRepository<HubDatabaseEntity>(context), IHubRepository
{
    public record HubListDto(
        string PublicId,
        string CommunityPublicId,
        string Name,
        string Slug,
        string? Description,
        bool AllowAnonymousReading,
        bool RequireEmailConfirmation,
        DateTime CreatedAt);

    public override async Task<HubDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(h => h.Community)
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<HubDatabaseEntity?> GetForUpdateAsync(string publicId)
    {
        return await _dbSet
            .Include(h => h.Community)
            .Include(h => h.Spaces)
            .FirstOrDefaultAsync(h => h.PublicId == publicId);
    }

    public override async Task<IEnumerable<HubDatabaseEntity>> GetAllAsync()
    {
        return await _dbSet
            .Include(h => h.Community)
            .ToListAsync();
    }

    public async Task<HubDetailDto?> GetForDisplayAsync(string publicId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(h => h.Community)
            .Where(h => h.PublicId == publicId)
            .Select(h => new HubDetailDto(
                h.PublicId,
                h.Community.PublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.AllowAnonymousReading,
                h.RequireEmailConfirmation,
                h.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<HubDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(h => h.Community)
            .FirstOrDefaultAsync(h => h.PublicId == publicId);
    }

    public async Task<HubDatabaseEntity?> GetBySlugAsync(string slug)
    {
        return await _dbSet
            .Include(h => h.Community)
            .FirstOrDefaultAsync(h => h.Slug == slug);
    }

    public async Task<PagedResult<HubListDto>> GetFilteredForDisplayAsync(
        int offset,
        int pageSize)
    {
        var items = await _dbSet
            .AsNoTracking()
            .Include(h => h.Community)
            .OrderBy(h => h.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(h => new HubListDto(
                h.PublicId,
                h.Community.PublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.AllowAnonymousReading,
                h.RequireEmailConfirmation,
                h.CreatedAt))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        return new PagedResult<HubListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<PagedResult<HubListDto>> GetByCommunityAsync(
        int communityId,
        int offset,
        int pageSize)
    {
        var items = await _dbSet
            .AsNoTracking()
            .Include(h => h.Community)
            .Where(h => h.CommunityId == communityId)
            .OrderBy(h => h.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(h => new HubListDto(
                h.PublicId,
                h.Community.PublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.AllowAnonymousReading,
                h.RequireEmailConfirmation,
                h.CreatedAt))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        return new PagedResult<HubListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<int?> GetCommunityDbIdAsync(string communityPublicId)
    {
        return await _context.Communities
            .Where(c => c.PublicId == communityPublicId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
    }
}
