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
        List<Discussion>? discussions = null)
    {
        PublicId = publicId;
        HubId = hubId;
        Name = name;
        Slug = slug;
        Description = description;
        AllowAnonymousReading = allowAnonymousReading;
        RequireEmailConfirmation = requireEmailConfirmation;
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
        List<Discussion>? discussions = null)
    {
        return new Space(
            publicId,
            hubId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            createdAt,
            lastModifiedAt,
            discussions);
    }

    public static Space RehydrateForList(
        SpaceId publicId,
        HubId hubId,
        string name,
        string slug,
        string? description,
        bool allowAnonymousReading,
        bool requireEmailConfirmation,
        DateTime createdAt)
    {
        return new Space(
            publicId,
            hubId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            createdAt,
            lastModifiedAt: null,
            discussions: []);
    }

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
}
