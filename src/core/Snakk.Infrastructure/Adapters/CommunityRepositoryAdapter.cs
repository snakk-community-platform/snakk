namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class CommunityRepositoryAdapter(
    Infrastructure.Database.Repositories.ICommunityDatabaseRepository databaseRepository,
    SnakkDbContext context,
    HybridCache cache) : Domain.Repositories.ICommunityRepository
{
    public async Task<Community?> GetByPublicIdAsync(CommunityId publicId, CancellationToken ct = default)
    {
        var projection = await context.Communities
            .Where(c => c.PublicId == publicId.Value)
            .Select(c => new CommunityProjection(
                c.PublicId, c.Name, c.Slug, c.Description,
                c.VisibilityId, c.ExposeToPlatformFeed,
                c.CreatedAt, c.LastModifiedAt,
                c.AvatarFileName, c.AvatarThumbnailFileName, c.AvatarMicroFileName, c.AvatarRevision, c.Require2FA,
                c.Timezone, c.LanguageCode))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Community?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var projection = await context.Communities
            .Where(c => c.Slug == slug)
            .Select(c => new CommunityProjection(
                c.PublicId, c.Name, c.Slug, c.Description,
                c.VisibilityId, c.ExposeToPlatformFeed,
                c.CreatedAt, c.LastModifiedAt,
                c.AvatarFileName, c.AvatarThumbnailFileName, c.AvatarMicroFileName, c.AvatarRevision, c.Require2FA,
                c.Timezone, c.LanguageCode))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Community?> GetByDomainAsync(string domain, CancellationToken ct = default)
    {
        var projection = await context.Set<Database.Entities.CommunityDomainDatabaseEntity>()
            .Where(d =>
                d.Domain == domain
                && d.IsVerified)
            .Select(d => new CommunityProjection(
                d.Community.PublicId, d.Community.Name, d.Community.Slug, d.Community.Description,
                d.Community.VisibilityId, d.Community.ExposeToPlatformFeed,
                d.Community.CreatedAt, d.Community.LastModifiedAt,
                d.Community.AvatarFileName, d.Community.AvatarThumbnailFileName, d.Community.AvatarMicroFileName, d.Community.AvatarRevision, d.Community.Require2FA,
                d.Community.Timezone, d.Community.LanguageCode))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Community>> GetPublicListedAsync(int offset, int pageSize, CancellationToken ct = default)
    {
        var result = await databaseRepository.GetPublicListedAsync(offset, pageSize, ct);

        return result.Map(dto => Community.RehydrateForList(
            CommunityId.From(dto.PublicId),
            dto.Name,
            dto.Slug,
            dto.Description,
            ((CommunityVisibilityEnum)dto.VisibilityId).ToDomain(),
            dto.ExposeToPlatformFeed,
            dto.CreatedAt,
            avatarFileName: dto.AvatarFileName));
    }

    public async Task<PagedResult<Community>> GetForPlatformFeedAsync(int offset, int pageSize, CancellationToken ct = default)
    {
        var result = await databaseRepository.GetForPlatformFeedAsync(offset, pageSize, ct);

        return result.Map(dto => Community.RehydrateForList(
            CommunityId.From(dto.PublicId),
            dto.Name,
            dto.Slug,
            dto.Description,
            ((CommunityVisibilityEnum)dto.VisibilityId).ToDomain(),
            dto.ExposeToPlatformFeed,
            dto.CreatedAt,
            avatarFileName: dto.AvatarFileName));
    }

    public async Task AddAsync(Community community, CancellationToken ct = default)
    {
        var entity = community.ToPersistence();
        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Community community, CancellationToken ct = default)
    {
        var entity = await context.Communities.FirstOrDefaultAsync(c => c.PublicId == community.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Community with PublicId '{community.PublicId}' not found");

        var nameChanged = entity.Name != community.Name;
        var slugChanged = entity.Slug != community.Slug;

        entity.Name = community.Name;
        entity.Slug = community.Slug;
        entity.Description = community.Description;
        entity.VisibilityId = (int)community.Visibility.ToShared();
        entity.ExposeToPlatformFeed = community.ExposeToPlatformFeed;
        entity.LastModifiedAt = community.LastModifiedAt;
        entity.AvatarFileName = community.AvatarFileName;
        entity.AvatarThumbnailFileName = community.AvatarThumbnailFileName;
        entity.AvatarMicroFileName = community.AvatarMicroFileName;
        entity.AvatarRevision = community.AvatarRevision;
        entity.Require2FA = community.Require2FA;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);

        if (nameChanged || slugChanged)
        {
            var affectedSpaceIds = await context.Spaces
                .Where(s => s.Hub.CommunityId == entity.Id)
                .Select(s => s.Id)
                .ToListAsync(ct);

            await context.Spaces
                .Where(s => s.Hub.CommunityId == entity.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(sp => sp.CommunityName, entity.Name)
                    .SetProperty(sp => sp.CommunitySlug, entity.Slug), ct);

            foreach (var id in affectedSpaceIds)
                await cache.RemoveAsync($"space-display:{id}", ct);
        }
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
        string? AvatarThumbnailFileName,
        string? AvatarMicroFileName,
        int AvatarRevision,
        bool Require2FA,
        string? Timezone,
        string? LanguageCode)
    {
        public Community ToDomain() => Community.Rehydrate(
            CommunityId.From(PublicId),
            Name, Slug, Description,
            ((CommunityVisibilityEnum)VisibilityId).ToDomain(),
            ExposeToPlatformFeed,
            CreatedAt, LastModifiedAt, hubs: [],
            timezone: Timezone,
            avatarFileName: AvatarFileName,
            avatarThumbnailFileName: AvatarThumbnailFileName,
            avatarMicroFileName: AvatarMicroFileName,
            avatarRevision: AvatarRevision,
            languageCode: LanguageCode,
            require2FA: Require2FA);
    }
}
