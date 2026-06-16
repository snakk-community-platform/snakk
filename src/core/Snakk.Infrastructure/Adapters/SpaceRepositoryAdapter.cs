namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class SpaceRepositoryAdapter(
    Infrastructure.Database.Repositories.ISpaceRepository databaseRepository,
    SnakkDbContext context,
    HybridCache cache) : Domain.Repositories.ISpaceRepository
{
    public async Task<Space?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var projection = await context.Spaces
            .Where(s => s.Id == id)
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId!, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent, s.Require2FA,
                s.LanguageCode, s.HubLanguageCode, s.CommunityLanguageCode))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Space?> GetByPublicIdAsync(SpaceId publicId, CancellationToken ct = default)
    {
        var projection = await context.Spaces
            .Where(s => s.PublicId == publicId.Value)
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId!, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent, s.Require2FA,
                s.LanguageCode, s.HubLanguageCode, s.CommunityLanguageCode))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Space?> GetBySlugAsync(string slug, string hubSlug, CancellationToken ct = default)
    {
        var projection = await context.Spaces
            .Where(s => s.Slug == slug && s.HubSlug == hubSlug)
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId!, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent, s.Require2FA,
                s.LanguageCode, s.HubLanguageCode, s.CommunityLanguageCode))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Space>> GetFilteredForDisplayAsync(
        HubId hubId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var result = await databaseRepository.GetFilteredForDisplayAsync(hubId.Value, offset, pageSize, ct);

        return result.Map(dto => Space.RehydrateForList(
            SpaceId.From(dto.PublicId),
            HubId.From(dto.HubPublicId),
            dto.Name,
            dto.Slug,
            dto.Description,
            dto.AllowAnonymousReading,
            dto.RequireEmailConfirmation,
            dto.CreatedAt));
    }

    public async Task<IEnumerable<Space>> GetAllAsync(CancellationToken ct = default)
    {
        var projections = await context.Spaces
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId!, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent, s.Require2FA,
                s.LanguageCode, s.HubLanguageCode, s.CommunityLanguageCode))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(Space space, CancellationToken ct = default)
    {
        var entity = space.ToPersistence();

        var hub = await context.Hubs
            .Include(h => h.Community)
            .FirstOrDefaultAsync(h => h.PublicId == space.HubId.Value, ct);

        if (hub is null)
            throw new InvalidOperationException($"Hub with PublicId '{space.HubId}' not found");

        entity.HubId = hub.Id;
        entity.HubPublicId = hub.PublicId;
        entity.HubSlug = hub.Slug;
        entity.HubName = hub.Name;
        entity.CommunityPublicId = hub.Community.PublicId;
        entity.CommunitySlug = hub.Community.Slug;
        entity.CommunityName = hub.Community.Name;

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);

        // Seed all discussion types as allowed for new space
        var allowedTypes = Enum.GetValues<DiscussionTypeEnum>()
            .Select(type => new SpaceAllowedDiscussionTypeDatabaseEntity
            {
                SpaceId = entity.Id,
                DiscussionType = (int)type
            });

        context.SpaceAllowedDiscussionTypes.AddRange(allowedTypes);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Space space, CancellationToken ct = default)
    {
        var entity = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == space.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Space with PublicId '{space.PublicId}' not found");

        entity.Name = space.Name;
        entity.Slug = space.Slug;
        entity.Description = space.Description;
        entity.AllowAnonymousReading = space.AllowAnonymousReading;
        entity.RequireEmailConfirmation = space.RequireEmailConfirmation;
        entity.AutoParagraphEnabled = space.AutoParagraphEnabled;
        entity.LastModifiedAt = space.LastModifiedAt;
        entity.AvatarFileName = space.AvatarFileName;
        entity.AvatarThumbnailFileName = space.AvatarThumbnailFileName;
        entity.AvatarMicroFileName = space.AvatarMicroFileName;
        entity.AvatarRevision = space.AvatarRevision;
        entity.IsAdultOnly = space.IsAdultOnly;
        entity.AllowsAdultContent = space.AllowsAdultContent;
        entity.Require2FA = space.Require2FA;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
        await cache.RemoveAsync($"space-display:{entity.Id}", ct);
    }

    private record SpaceProjection(
        string PublicId,
        string HubPublicId,
        string Name,
        string Slug,
        string? Description,
        bool AllowAnonymousReading,
        bool RequireEmailConfirmation,
        DateTime CreatedAt,
        DateTime? LastModifiedAt,
        string? AvatarFileName,
        string? AvatarThumbnailFileName,
        string? AvatarMicroFileName,
        int AvatarRevision,
        bool AutoParagraphEnabled,
        bool IsAdultOnly,
        bool AllowsAdultContent,
        bool Require2FA,
        string? LanguageCode,
        string? HubLanguageCode,
        string? CommunityLanguageCode)
    {
        public Space ToDomain() => Space.Rehydrate(
            SpaceId.From(PublicId),
            HubId.From(HubPublicId),
            Name, Slug, Description,
            AllowAnonymousReading, RequireEmailConfirmation,
            CreatedAt, LastModifiedAt, discussions: [],
            avatarFileName: AvatarFileName,
            avatarThumbnailFileName: AvatarThumbnailFileName,
            avatarMicroFileName: AvatarMicroFileName,
            avatarRevision: AvatarRevision,
            languageCode: LanguageCode,
            hubLanguageCode: HubLanguageCode,
            communityLanguageCode: CommunityLanguageCode,
            autoParagraphEnabled: AutoParagraphEnabled,
            isAdultOnly: IsAdultOnly,
            allowsAdultContent: AllowsAdultContent,
            require2FA: Require2FA);
    }
}
