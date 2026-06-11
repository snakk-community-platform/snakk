namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Extensions;
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

    public override async Task<HubDatabaseEntity?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<HubDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .AsTracking()
        .Include(h => h.Community)
        .FirstOrDefaultAsync(h => h.PublicId == publicId, ct);

    public override async Task<IEnumerable<HubDatabaseEntity>> GetAllAsync(CancellationToken ct = default) =>
        await _dbSet.AsNoTracking()
            .Include(h => h.Community)
            .Take(1000)
            .ToListAsync(ct);

    public async Task<HubDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Where(h => h.PublicId == publicId)
        .Select(h => new HubDetailDto(
            h.PublicId,
            h.CommunityPublicId,
            h.Name,
            h.Slug,
            h.Description,
            h.AllowAnonymousReading,
            h.RequireEmailConfirmation,
            h.CreatedAt))
        .FirstOrDefaultAsync(ct);

    public async Task<HubDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(h => h.PublicId == publicId, ct);

    public async Task<HubDatabaseEntity?> GetBySlugAsync(string slug, string communitySlug, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(h => h.Slug == slug && h.Community.Slug == communitySlug, ct);

    public async Task<PagedResult<HubListDto>> GetFilteredForDisplayAsync(
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        return await _dbSet
            .OrderBy(h => h.Name)
            .Select(h => new HubListDto(
                h.PublicId,
                h.CommunityPublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.AllowAnonymousReading,
                h.RequireEmailConfirmation,
                h.CreatedAt))
            .ToPagedResultAsync(offset, pageSize, ct);
    }

    public async Task<PagedResult<HubListDto>> GetByCommunityAsync(
        int communityId,
        int offset,
        int pageSize,
        string? userId = null,
        CancellationToken ct = default)
    {
        var query = _dbSet
            .Where(h => h.CommunityId == communityId);

        query = await WithHubAccessFilterAsync(query, userId);

        return await query
            .OrderBy(h => h.Name)
            .Select(h => new HubListDto(
                h.PublicId,
                h.CommunityPublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.AllowAnonymousReading,
                h.RequireEmailConfirmation,
                h.CreatedAt))
            .ToPagedResultAsync(offset, pageSize, ct);
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

    public async Task<int?> GetCommunityDbIdAsync(string communityPublicId, CancellationToken ct = default) => await _context.Communities
        .Where(c => c.PublicId == communityPublicId)
        .Select(c => (int?)c.Id)
        .FirstOrDefaultAsync(ct);
}
