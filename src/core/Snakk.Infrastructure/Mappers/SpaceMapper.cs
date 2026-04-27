namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class SpaceMapper
{
    public static Space FromPersistence(this SpaceDatabaseEntity entity) =>
        Space.Rehydrate(
            SpaceId.From(entity.PublicId),
            HubId.From(entity.Hub.PublicId),
            entity.Name,
            entity.Slug,
            entity.Description,
            entity.AllowAnonymousReading,
            entity.RequireEmailConfirmation,
            entity.CreatedAt,
            entity.LastModifiedAt,
            discussions: [],
            avatarFileName: entity.AvatarFileName,
            avatarRevision: entity.AvatarRevision,
            languageCode: entity.LanguageCode,
            hubLanguageCode: entity.HubLanguageCode,
            communityLanguageCode: entity.CommunityLanguageCode,
            autoParagraphEnabled: entity.AutoParagraphEnabled,
            isAdultOnly: entity.IsAdultOnly,
            allowsAdultContent: entity.AllowsAdultContent);

    public static SpaceDatabaseEntity ToPersistence(this Space space) =>
        new()
        {
            PublicId = space.PublicId,
            Name = space.Name,
            Slug = space.Slug,
            Description = space.Description,
            AllowAnonymousReading = space.AllowAnonymousReading,
            RequireEmailConfirmation = space.RequireEmailConfirmation,
            AutoParagraphEnabled = space.AutoParagraphEnabled,
            CreatedAt = space.CreatedAt,
            LastModifiedAt = space.LastModifiedAt,
            AvatarFileName = space.AvatarFileName,
            AvatarRevision = space.AvatarRevision,
            LanguageCode = space.LanguageCode,
            HubLanguageCode = space.HubLanguageCode,
            CommunityLanguageCode = space.CommunityLanguageCode,
            IsAdultOnly = space.IsAdultOnly,
            AllowsAdultContent = space.AllowsAdultContent
            // HubId will be set by repository adapter
        };
}
