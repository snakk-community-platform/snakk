namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class SpaceRepositoryAdapter(
    Infrastructure.Database.Repositories.ISpaceRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.ISpaceRepository
{
    public async Task<Space?> GetByIdAsync(int id)
    {
        var projection = await context.Spaces
            .Where(s => s.Id == id)
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Space?> GetByPublicIdAsync(SpaceId publicId)
    {
        var projection = await context.Spaces
            .Where(s => s.PublicId == publicId.Value)
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Space?> GetBySlugAsync(string slug, string hubSlug)
    {
        var projection = await context.Spaces
            .Where(s => s.Slug == slug && s.HubSlug == hubSlug)
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Space>> GetFilteredForDisplayAsync(
        HubId hubId,
        int offset,
        int pageSize)
    {
        var result = await databaseRepository.GetFilteredForDisplayAsync(hubId.Value, offset, pageSize);

        return new PagedResult<Space>
        {
            Items = result.Items
                .Select(dto => Space.RehydrateForList(
                    SpaceId.From(dto.PublicId),
                    HubId.From(dto.HubPublicId),
                    dto.Name,
                    dto.Slug,
                    dto.Description,
                    dto.AllowAnonymousReading,
                    dto.RequireEmailConfirmation,
                    dto.CreatedAt))
                .ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<IEnumerable<Space>> GetAllAsync()
    {
        var projections = await context.Spaces
            .Select(s => new SpaceProjection(
                s.PublicId, s.HubPublicId, s.Name, s.Slug, s.Description,
                s.AllowAnonymousReading, s.RequireEmailConfirmation,
                s.CreatedAt, s.LastModifiedAt,
                s.AvatarFileName, s.AvatarThumbnailFileName, s.AvatarMicroFileName, s.AvatarRevision,
                s.AutoParagraphEnabled, s.IsAdultOnly, s.AllowsAdultContent))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddAsync(Space space)
    {
        var entity = space.ToPersistence();

        var hub = await context.Hubs
            .Include(h => h.Community)
            .FirstOrDefaultAsync(h => h.PublicId == space.HubId.Value);

        if (hub is null)
            throw new InvalidOperationException($"Hub with PublicId '{space.HubId}' not found");

        entity.HubId = hub.Id;
        entity.HubPublicId = hub.PublicId;
        entity.HubSlug = hub.Slug;
        entity.HubName = hub.Name;
        entity.CommunityPublicId = hub.Community.PublicId;
        entity.CommunitySlug = hub.Community.Slug;
        entity.CommunityName = hub.Community.Name;

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();

        // Seed all discussion types as allowed for new space
        var allowedTypes = Enum.GetValues<DiscussionTypeEnum>()
            .Select(type => new SpaceAllowedDiscussionTypeDatabaseEntity
            {
                SpaceId = entity.Id,
                DiscussionType = (int)type
            });

        context.SpaceAllowedDiscussionTypes.AddRange(allowedTypes);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Space space)
    {
        var entity = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == space.PublicId.Value);

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

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
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
        bool AllowsAdultContent)
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
            autoParagraphEnabled: AutoParagraphEnabled,
            isAdultOnly: IsAdultOnly,
            allowsAdultContent: AllowsAdultContent);
    }
}
