namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class Space
{
    public SpaceId PublicId { get; private set; }
    public HubId HubId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Slug { get; private set; }
    public bool AllowAnonymousReading { get; private set; }
    public bool RequireEmailConfirmation { get; private set; }
    public bool AutoParagraphEnabled { get; private set; }
    public string? LanguageCode { get; private set; }
    public string? HubLanguageCode { get; private set; }
    public string? CommunityLanguageCode { get; private set; }
    public string? AvatarFileName { get; private set; }
    public string? AvatarThumbnailFileName { get; private set; }
    public string? AvatarMicroFileName { get; private set; }
    public int AvatarRevision { get; private set; }
    public bool IsAdultOnly { get; private set; }
    public bool AllowsAdultContent { get; private set; }
    public bool Require2FA { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }

    private readonly List<Discussion> _discussions = [];
    public IReadOnlyCollection<Discussion> Discussions => _discussions.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Space()
    {
        _discussions = [];
    }
#pragma warning restore CS8618

    private Space(
        SpaceId publicId,
        HubId hubId,
        string name,
        string slug,
        string? description,
        bool allowAnonymousReading,
        bool requireEmailConfirmation,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        List<Discussion>? discussions = null,
        string? avatarFileName = null,
        string? avatarThumbnailFileName = null,
        string? avatarMicroFileName = null,
        int avatarRevision = 0,
        string? languageCode = null,
        string? hubLanguageCode = null,
        string? communityLanguageCode = null,
        bool autoParagraphEnabled = true,
        bool isAdultOnly = false,
        bool allowsAdultContent = false,
        bool require2FA = false)
    {
        PublicId = publicId;
        HubId = hubId;
        Name = name;
        Slug = slug;
        Description = description;
        AllowAnonymousReading = allowAnonymousReading;
        RequireEmailConfirmation = requireEmailConfirmation;
        AutoParagraphEnabled = autoParagraphEnabled;
        LanguageCode = languageCode;
        HubLanguageCode = hubLanguageCode;
        CommunityLanguageCode = communityLanguageCode;
        AvatarFileName = avatarFileName;
        AvatarThumbnailFileName = avatarThumbnailFileName;
        AvatarMicroFileName = avatarMicroFileName;
        AvatarRevision = avatarRevision;
        IsAdultOnly = isAdultOnly;
        AllowsAdultContent = allowsAdultContent;
        Require2FA = require2FA;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        _discussions = discussions ?? [];
    }

    public static Space Create(
        HubId hubId,
        string name,
        string slug,
        string? description = null,
        bool allowAnonymousReading = true,
        bool requireEmailConfirmation = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Space name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Space slug cannot be empty", nameof(slug));

        return new Space(
            SpaceId.New(),
            hubId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            DateTime.UtcNow);
    }

    public static Space Rehydrate(
        SpaceId publicId,
        HubId hubId,
        string name,
        string slug,
        string? description,
        bool allowAnonymousReading,
        bool requireEmailConfirmation,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        List<Discussion>? discussions = null,
        string? avatarFileName = null,
        string? avatarThumbnailFileName = null,
        string? avatarMicroFileName = null,
        int avatarRevision = 0,
        string? languageCode = null,
        string? hubLanguageCode = null,
        string? communityLanguageCode = null,
        bool autoParagraphEnabled = true,
        bool isAdultOnly = false,
        bool allowsAdultContent = false,
        bool require2FA = false) =>
        new Space(
            publicId,
            hubId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            createdAt,
            lastModifiedAt,
            discussions,
            avatarFileName,
            avatarThumbnailFileName,
            avatarMicroFileName,
            avatarRevision,
            languageCode,
            hubLanguageCode,
            communityLanguageCode,
            autoParagraphEnabled,
            isAdultOnly,
            allowsAdultContent,
            require2FA);

    public static Space RehydrateForList(
        SpaceId publicId,
        HubId hubId,
        string name,
        string slug,
        string? description,
        bool allowAnonymousReading,
        bool requireEmailConfirmation,
        DateTime createdAt,
        string? languageCode = null,
        string? hubLanguageCode = null,
        string? communityLanguageCode = null) =>
        new Space(
            publicId,
            hubId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            createdAt,
            lastModifiedAt: null,
            discussions: [],
            languageCode: languageCode,
            hubLanguageCode: hubLanguageCode,
            communityLanguageCode: communityLanguageCode);

    public void UpdateLanguageCode(string? languageCode)
    {
        LanguageCode = languageCode;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateHubLanguageCode(string? hubLanguageCode)
    {
        HubLanguageCode = hubLanguageCode;
    }

    public void UpdateCommunityLanguageCode(string? communityLanguageCode)
    {
        CommunityLanguageCode = communityLanguageCode;
    }

    public string EffectiveLanguageCode =>
        LanguageCode ?? HubLanguageCode ?? CommunityLanguageCode ?? "en";

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Space name cannot be empty", nameof(name));

        Name = name;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Space slug cannot be empty", nameof(slug));

        Slug = slug;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetAvatarFileName(string? fileName, string? thumbnailFileName = null, string? microFileName = null)
    {
        AvatarFileName = fileName;
        AvatarThumbnailFileName = thumbnailFileName;
        AvatarMicroFileName = microFileName;
        AvatarRevision++;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void ClearAvatar()
    {
        AvatarFileName = null;
        AvatarThumbnailFileName = null;
        AvatarMicroFileName = null;
        AvatarRevision++;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetAutoParagraphEnabled(bool enabled)
    {
        AutoParagraphEnabled = enabled;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetRequire2FA(bool require)
    {
        Require2FA = require;
        LastModifiedAt = DateTime.UtcNow;
    }
}
