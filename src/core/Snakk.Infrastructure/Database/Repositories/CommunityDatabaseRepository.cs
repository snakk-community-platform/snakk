namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class CommunityDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<CommunityDatabaseEntity>(context), ICommunityDatabaseRepository
{
    public async Task<CommunityDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.PublicId == publicId);
    }

    public async Task<CommunityDatabaseEntity?> GetBySlugAsync(string slug)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.Slug == slug);
    }

    public async Task<CommunityDatabaseEntity?> GetByDomainAsync(string domain)
    {
        return await _context.Set<CommunityDomainDatabaseEntity>()
            .Where(d => d.Domain == domain && d.IsVerified)
            .Select(d => d.Community)
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<CommunityListDto>> GetPublicListedAsync(int offset, int pageSize)
    {
        var items = await _dbSet
            .AsNoTracking()
            .Where(c => c.Visibility == "PublicListed")
            .OrderBy(c => c.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(c => new CommunityListDto(
                c.PublicId,
                c.Name,
                c.Slug,
                c.Description,
                c.Visibility,
                c.ExposeToPlatformFeed,
                c.CreatedAt))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        return new PagedResult<CommunityListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<PagedResult<CommunityListDto>> GetForPlatformFeedAsync(int offset, int pageSize)
    {
        var items = await _dbSet
            .AsNoTracking()
            .Where(c => c.ExposeToPlatformFeed)
            .OrderBy(c => c.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(c => new CommunityListDto(
                c.PublicId,
                c.Name,
                c.Slug,
                c.Description,
                c.Visibility,
                c.ExposeToPlatformFeed,
                c.CreatedAt))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        return new PagedResult<CommunityListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }
}
