namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class Hub
{
    public HubId PublicId { get; private set; }
    public CommunityId CommunityId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Slug { get; private set; }
    public bool AllowAnonymousReading { get; private set; }
    public bool RequireEmailConfirmation { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }

    private readonly List<Space> _spaces = [];
    public IReadOnlyCollection<Space> Spaces => _spaces.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Hub()
    {
        _spaces = [];
    }
#pragma warning restore CS8618

    private Hub(
        HubId publicId,
        CommunityId communityId,
        string name,
        string slug,
        string? description,
        bool allowAnonymousReading,
        bool requireEmailConfirmation,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        List<Space>? spaces = null)
    {
        PublicId = publicId;
        CommunityId = communityId;
        Name = name;
        Slug = slug;
        Description = description;
        AllowAnonymousReading = allowAnonymousReading;
        RequireEmailConfirmation = requireEmailConfirmation;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        _spaces = spaces ?? [];
    }

    public static Hub Create(
        CommunityId communityId,
        string name,
        string slug,
        string? description = null,
        bool allowAnonymousReading = true,
        bool requireEmailConfirmation = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Hub name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Hub slug cannot be empty", nameof(slug));

        return new Hub(
            HubId.New(),
            communityId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            DateTime.UtcNow);
    }

    public static Hub Rehydrate(
        HubId publicId,
        CommunityId communityId,
        string name,
        string slug,
        string? description,
        bool allowAnonymousReading,
        bool requireEmailConfirmation,
        DateTime createdAt,
        DateTime? lastModifiedAt = null,
        List<Space>? spaces = null)
    {
        return new Hub(
            publicId,
            communityId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            createdAt,
            lastModifiedAt,
            spaces);
    }

    public static Hub RehydrateForList(
        HubId publicId,
        CommunityId communityId,
        string name,
        string slug,
        string? description,
        bool allowAnonymousReading,
        bool requireEmailConfirmation,
        DateTime createdAt)
    {
        return new Hub(
            publicId,
            communityId,
            name,
            slug,
            description,
            allowAnonymousReading,
            requireEmailConfirmation,
            createdAt,
            lastModifiedAt: null,
            spaces: []);
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Hub name cannot be empty", nameof(name));

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
            throw new ArgumentException("Hub slug cannot be empty", nameof(slug));

        Slug = slug;
        LastModifiedAt = DateTime.UtcNow;
    }
}
