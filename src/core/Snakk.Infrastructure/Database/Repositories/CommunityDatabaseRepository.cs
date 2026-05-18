namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class CommunityDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<CommunityDatabaseEntity>(context), ICommunityDatabaseRepository
{
    public async Task<CommunityDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(c => c.PublicId == publicId, ct);

    public async Task<CommunityDatabaseEntity?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public async Task<CommunityDatabaseEntity?> GetByDomainAsync(string domain, CancellationToken ct = default) => await _context.Set<CommunityDomainDatabaseEntity>()
        .Where(d => d.Domain == domain && d.IsVerified)
        .Select(d => d.Community)
        .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<CommunityListDto>> GetPublicListedAsync(
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var items = await _dbSet
            .Where(c => c.VisibilityId == (int)CommunityVisibilityEnum.PublicListed)
            .OrderBy(c => c.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(c => new CommunityListDto(
                c.PublicId,
                c.Name,
                c.Slug,
                c.Description,
                c.VisibilityId,
                c.ExposeToPlatformFeed,
                c.CreatedAt,
                c.AvatarFileName))
            .ToListAsync(ct);

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        return new PagedResult<CommunityListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<PagedResult<CommunityListDto>> GetForPlatformFeedAsync(
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var items = await _dbSet
            .Where(c => c.ExposeToPlatformFeed)
            .OrderBy(c => c.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(c => new CommunityListDto(
                c.PublicId,
                c.Name,
                c.Slug,
                c.Description,
                c.VisibilityId,
                c.ExposeToPlatformFeed,
                c.CreatedAt,
                c.AvatarFileName))
            .ToListAsync(ct);

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        return new PagedResult<CommunityListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }
}
