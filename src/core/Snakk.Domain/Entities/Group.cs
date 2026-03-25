namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class Group
{
    public GroupId PublicId { get; private set; }
    public CommunityId CommunityId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? Description { get; private set; }
    public bool IsPublic { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private Group() { }
#pragma warning restore CS8618

    private Group(
        GroupId publicId,
        CommunityId communityId,
        string name,
        string slug,
        string? description,
        bool isPublic,
        int sortOrder,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        PublicId = publicId;
        CommunityId = communityId;
        Name = name;
        Slug = slug;
        Description = description;
        IsPublic = isPublic;
        SortOrder = sortOrder;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Group Create(
        CommunityId communityId,
        string name,
        string slug,
        string? description = null,
        bool isPublic = true,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Group slug cannot be empty", nameof(slug));

        return new Group(
            GroupId.New(),
            communityId,
            name,
            slug,
            description,
            isPublic,
            sortOrder,
            DateTime.UtcNow);
    }

    public static Group Rehydrate(
        GroupId publicId,
        CommunityId communityId,
        string name,
        string slug,
        string? description,
        bool isPublic,
        int sortOrder,
        DateTime createdAt,
        DateTime? updatedAt = null) =>
        new Group(
            publicId,
            communityId,
            name,
            slug,
            description,
            isPublic,
            sortOrder,
            createdAt,
            updatedAt);

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty", nameof(name));

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPublic(bool isPublic)
    {
        IsPublic = isPublic;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
