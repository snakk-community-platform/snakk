namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class CommunityRepositoryAdapter(
    Infrastructure.Database.Repositories.ICommunityDatabaseRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.ICommunityRepository
{
    private readonly Infrastructure.Database.Repositories.ICommunityDatabaseRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Community?> GetByPublicIdAsync(CommunityId publicId)
    {
        var projection = await _context.Communities
            .Where(c => c.PublicId == publicId.Value)
            .Select(c => new CommunityProjection(
                c.PublicId, c.Name, c.Slug, c.Description,
                c.VisibilityId, c.ExposeToPlatformFeed,
                c.CreatedAt, c.LastModifiedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Community?> GetBySlugAsync(string slug)
    {
        var projection = await _context.Communities
            .Where(c => c.Slug == slug)
            .Select(c => new CommunityProjection(
                c.PublicId, c.Name, c.Slug, c.Description,
                c.VisibilityId, c.ExposeToPlatformFeed,
                c.CreatedAt, c.LastModifiedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Community?> GetByDomainAsync(string domain)
    {
        var projection = await _context.Set<Database.Entities.CommunityDomainDatabaseEntity>()
            .Where(d => d.Domain == domain && d.IsVerified)
            .Select(d => new CommunityProjection(
                d.Community.PublicId, d.Community.Name, d.Community.Slug, d.Community.Description,
                d.Community.VisibilityId, d.Community.ExposeToPlatformFeed,
                d.Community.CreatedAt, d.Community.LastModifiedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Community>> GetPublicListedAsync(int offset, int pageSize)
    {
        var result = await _databaseRepository.GetPublicListedAsync(offset, pageSize);
        return new PagedResult<Community>
        {
            Items = result.Items.Select(dto => Community.RehydrateForList(
                CommunityId.From(dto.PublicId),
                dto.Name,
                dto.Slug,
                dto.Description,
                ((CommunityVisibilityEnum)dto.VisibilityId).ToDomain(),
                dto.ExposeToPlatformFeed,
                dto.CreatedAt)).ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<PagedResult<Community>> GetForPlatformFeedAsync(int offset, int pageSize)
    {
        var result = await _databaseRepository.GetForPlatformFeedAsync(offset, pageSize);
        return new PagedResult<Community>
        {
            Items = result.Items.Select(dto => Community.RehydrateForList(
                CommunityId.From(dto.PublicId),
                dto.Name,
                dto.Slug,
                dto.Description,
                ((CommunityVisibilityEnum)dto.VisibilityId).ToDomain(),
                dto.ExposeToPlatformFeed,
                dto.CreatedAt)).ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task AddAsync(Community community)
    {
        var entity = community.ToPersistence();
        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Community community)
    {
        var entity = await _context.Communities.FirstOrDefaultAsync(c => c.PublicId == community.PublicId.Value);
        if (entity == null)
            throw new InvalidOperationException($"Community with PublicId '{community.PublicId}' not found");

        entity.Name = community.Name;
        entity.Slug = community.Slug;
        entity.Description = community.Description;
        entity.VisibilityId = (int)community.Visibility.ToShared();
        entity.ExposeToPlatformFeed = community.ExposeToPlatformFeed;
        entity.LastModifiedAt = community.LastModifiedAt;

        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    private record CommunityProjection(
        string PublicId,
        string Name,
        string Slug,
        string? Description,
        int VisibilityId,
        bool ExposeToPlatformFeed,
        DateTime CreatedAt,
        DateTime? LastModifiedAt)
    {
        public Community ToDomain() => Community.Rehydrate(
            CommunityId.From(PublicId),
            Name, Slug, Description,
            ((CommunityVisibilityEnum)VisibilityId).ToDomain(),
            ExposeToPlatformFeed,
            CreatedAt, LastModifiedAt, hubs: []);
    }
}
