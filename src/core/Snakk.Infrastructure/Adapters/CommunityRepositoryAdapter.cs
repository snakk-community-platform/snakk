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
    public async Task<Community?> GetByPublicIdAsync(CommunityId publicId)
    {
        var projection = await context.Communities
            .Where(c => c.PublicId == publicId.Value)
            .Select(c => new CommunityProjection(
                c.PublicId, c.Name, c.Slug, c.Description,
                c.VisibilityId, c.ExposeToPlatformFeed,
                c.CreatedAt, c.LastModifiedAt,
                c.AvatarFileName, c.AvatarRevision))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Community?> GetBySlugAsync(string slug)
    {
        var projection = await context.Communities
            .Where(c => c.Slug == slug)
            .Select(c => new CommunityProjection(
                c.PublicId, c.Name, c.Slug, c.Description,
                c.VisibilityId, c.ExposeToPlatformFeed,
                c.CreatedAt, c.LastModifiedAt,
                c.AvatarFileName, c.AvatarRevision))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Community?> GetByDomainAsync(string domain)
    {
        var projection = await context.Set<Database.Entities.CommunityDomainDatabaseEntity>()
            .Where(d =>
                d.Domain == domain
                && d.IsVerified)
            .Select(d => new CommunityProjection(
                d.Community.PublicId, d.Community.Name, d.Community.Slug, d.Community.Description,
                d.Community.VisibilityId, d.Community.ExposeToPlatformFeed,
                d.Community.CreatedAt, d.Community.LastModifiedAt,
                d.Community.AvatarFileName, d.Community.AvatarRevision))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Community>> GetPublicListedAsync(int offset, int pageSize)
    {
        var result = await databaseRepository.GetPublicListedAsync(offset, pageSize);

        return new PagedResult<Community>
        {
            Items = result.Items
                .Select(dto => Community.RehydrateForList(
                    CommunityId.From(dto.PublicId),
                    dto.Name,
                    dto.Slug,
                    dto.Description,
                    ((CommunityVisibilityEnum)dto.VisibilityId).ToDomain(),
                    dto.ExposeToPlatformFeed,
                    dto.CreatedAt))
                .ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<PagedResult<Community>> GetForPlatformFeedAsync(int offset, int pageSize)
    {
        var result = await databaseRepository.GetForPlatformFeedAsync(offset, pageSize);

        return new PagedResult<Community>
        {
            Items = result.Items
                .Select(dto => Community.RehydrateForList(
                    CommunityId.From(dto.PublicId),
                    dto.Name,
                    dto.Slug,
                    dto.Description,
                    ((CommunityVisibilityEnum)dto.VisibilityId).ToDomain(),
                    dto.ExposeToPlatformFeed,
                    dto.CreatedAt))
                .ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task AddAsync(Community community)
    {
        var entity = community.ToPersistence();
        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Community community)
    {
        var entity = await context.Communities.FirstOrDefaultAsync(c => c.PublicId == community.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Community with PublicId '{community.PublicId}' not found");

        entity.Name = community.Name;
        entity.Slug = community.Slug;
        entity.Description = community.Description;
        entity.VisibilityId = (int)community.Visibility.ToShared();
        entity.ExposeToPlatformFeed = community.ExposeToPlatformFeed;
        entity.LastModifiedAt = community.LastModifiedAt;
        entity.AvatarFileName = community.AvatarFileName;
        entity.AvatarRevision = community.AvatarRevision;

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    private record CommunityProjection(
        string PublicId,
        string Name,
        string Slug,
        string? Description,
        int VisibilityId,
        bool ExposeToPlatformFeed,
        DateTime CreatedAt,
        DateTime? LastModifiedAt,
        string? AvatarFileName,
        int AvatarRevision)
    {
        public Community ToDomain() => Community.Rehydrate(
            CommunityId.From(PublicId),
            Name, Slug, Description,
            ((CommunityVisibilityEnum)VisibilityId).ToDomain(),
            ExposeToPlatformFeed,
            CreatedAt, LastModifiedAt, hubs: [],
            avatarFileName: AvatarFileName,
            avatarRevision: AvatarRevision);
    }
}
