namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class Community
{
    public CommunityId PublicId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? Description { get; private set; }
    public CommunityVisibility Visibility { get; private set; }
    public bool ExposeToPlatformFeed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }

    private readonly List<Hub> _hubs = [];
    public IReadOnlyCollection<Hub> Hubs => _hubs.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Community()
    {
        _hubs = [];
    }
#pragma warning restore CS8618

    private Community(
        CommunityId publicId,
        string name,
        string slug,
        string? description,
        CommunityVisibility visibility,
        bool exposeToPlatformFeed,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        List<Hub>? hubs = null)
    {
        PublicId = publicId;
        Name = name;
        Slug = slug;
        Description = description;
        Visibility = visibility;
        ExposeToPlatformFeed = exposeToPlatformFeed;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        _hubs = hubs ?? [];
    }

    public static Community Create(
        string name,
        string slug,
        string? description = null,
        CommunityVisibility visibility = CommunityVisibility.PublicListed,
        bool exposeToPlatformFeed = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Community name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Community slug cannot be empty", nameof(slug));

        return new Community(
            CommunityId.New(),
            name,
            slug,
            description,
            visibility,
            exposeToPlatformFeed,
            DateTime.UtcNow);
    }

    public static Community Rehydrate(
        CommunityId publicId,
        string name,
        string slug,
        string? description,
        CommunityVisibility visibility,
        bool exposeToPlatformFeed,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        List<Hub>? hubs = null)
    {
        return new Community(
            publicId,
            name,
            slug,
            description,
            visibility,
            exposeToPlatformFeed,
            createdAt,
            lastModifiedAt,
            hubs);
    }

    public static Community RehydrateForList(
        CommunityId publicId,
        string name,
        string slug,
        string? description,
        CommunityVisibility visibility,
        bool exposeToPlatformFeed,
        DateTime createdAt)
    {
        return new Community(
            publicId,
            name,
            slug,
            description,
            visibility,
            exposeToPlatformFeed,
            createdAt,
            lastModifiedAt: null,
            hubs: []);
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Community name cannot be empty", nameof(name));

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
            throw new ArgumentException("Community slug cannot be empty", nameof(slug));

        Slug = slug;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void UpdateVisibility(CommunityVisibility visibility)
    {
        Visibility = visibility;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void SetExposeToPlatformFeed(bool expose)
    {
        ExposeToPlatformFeed = expose;
        LastModifiedAt = DateTime.UtcNow;
    }
}
