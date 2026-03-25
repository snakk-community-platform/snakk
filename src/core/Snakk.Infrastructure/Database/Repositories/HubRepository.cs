namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Models;

public class HubRepository(SnakkDbContext context, IUserGrantsCacheService grantsCache)
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

    public override async Task<HubDatabaseEntity?> GetByIdAsync(int id) =>
        await _dbSet.FirstOrDefaultAsync(h => h.Id == id);

    public async Task<HubDatabaseEntity?> GetForUpdateAsync(string publicId) => await _dbSet
        .AsTracking()
        .Include(h => h.Community)
        .FirstOrDefaultAsync(h => h.PublicId == publicId);

    public override async Task<IEnumerable<HubDatabaseEntity>> GetAllAsync() =>
        await _dbSet.ToListAsync();

    public async Task<HubDetailDto?> GetForDisplayAsync(string publicId) => await _dbSet
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

    public async Task<HubDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet.FirstOrDefaultAsync(h => h.PublicId == publicId);

    public async Task<HubDatabaseEntity?> GetBySlugAsync(string slug, string communitySlug) =>
        await _dbSet.FirstOrDefaultAsync(h => h.Slug == slug && h.Community.Slug == communitySlug);

    public async Task<PagedResult<HubListDto>> GetFilteredForDisplayAsync(
        int offset,
        int pageSize)
    {
        var items = await _dbSet
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
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

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
        int pageSize,
        string? userId = null)
    {
        var query = _dbSet
            .Where(h => h.CommunityId == communityId);

        query = await WithHubAccessFilterAsync(query, userId);

        var items = await query
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
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        return new PagedResult<HubListDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    private async Task<IQueryable<HubDatabaseEntity>> WithHubAccessFilterAsync(
        IQueryable<HubDatabaseEntity> query, string? userId)
    {
        if (!await grantsCache.AnyRestrictedAsync())
            return query;

        if (userId == null)
            return query.Where(h =>
                !h.IsRestricted &&
                !h.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId);
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(h =>
            (!h.IsRestricted || hubIds.Contains(h.Id))
            && (!h.Community.IsRestricted || communityIds.Contains(h.CommunityId)));
    }

    public async Task<int?> GetCommunityDbIdAsync(string communityPublicId) => await _context.Communities
        .Where(c => c.PublicId == communityPublicId)
        .Select(c => (int?)c.Id)
        .FirstOrDefaultAsync();
}
