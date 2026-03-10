namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public class Announcement
{
    public AnnouncementId PublicId { get; private set; }
    public string Title { get; private set; }
    public string Content { get; private set; }
    public string RenderedContent { get; private set; }
    public AnnouncementTypeEnum Type { get; private set; }
    public AnnouncementScopeEnum Scope { get; private set; }
    public string ScopeEntityId { get; private set; }
    public DateTime? VisibleFrom { get; private set; }
    public DateTime? VisibleUntil { get; private set; }
    public bool IsDismissible { get; private set; }
    public int SortOrder { get; private set; }
    public UserId CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Announcement() { }
#pragma warning restore CS8618

    private Announcement(
        AnnouncementId publicId,
        string title,
        string content,
        string renderedContent,
        AnnouncementTypeEnum type,
        AnnouncementScopeEnum scope,
        string scopeEntityId,
        DateTime? visibleFrom,
        DateTime? visibleUntil,
        bool isDismissible,
        int sortOrder,
        UserId createdByUserId,
        DateTime createdAt,
        DateTime? lastModifiedAt)
    {
        PublicId = publicId;
        Title = title;
        Content = content;
        RenderedContent = renderedContent;
        Type = type;
        Scope = scope;
        ScopeEntityId = scopeEntityId;
        VisibleFrom = visibleFrom;
        VisibleUntil = visibleUntil;
        IsDismissible = isDismissible;
        SortOrder = sortOrder;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
    }

    public static Announcement Create(
        AnnouncementScopeEnum scope,
        string scopeEntityId,
        UserId createdByUserId,
        string title,
        string content,
        string renderedContent,
        AnnouncementTypeEnum type = AnnouncementTypeEnum.Info,
        DateTime? visibleFrom = null,
        DateTime? visibleUntil = null,
        bool isDismissible = true,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Announcement title cannot be empty", nameof(title));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Announcement content cannot be empty", nameof(content));

        if (string.IsNullOrWhiteSpace(scopeEntityId))
            throw new ArgumentException("Scope entity ID cannot be empty", nameof(scopeEntityId));

        if (visibleFrom.HasValue && visibleUntil.HasValue && visibleFrom >= visibleUntil)
            throw new ArgumentException("VisibleFrom must be before VisibleUntil");

        return new Announcement(
            AnnouncementId.New(),
            title,
            content,
            renderedContent,
            type,
            scope,
            scopeEntityId,
            visibleFrom,
            visibleUntil,
            isDismissible,
            sortOrder,
            createdByUserId,
            DateTime.UtcNow,
            lastModifiedAt: null);
    }

    public static Announcement Rehydrate(
        AnnouncementId publicId,
        string title,
        string content,
        string renderedContent,
        AnnouncementTypeEnum type,
        AnnouncementScopeEnum scope,
        string scopeEntityId,
        DateTime? visibleFrom,
        DateTime? visibleUntil,
        bool isDismissible,
        int sortOrder,
        UserId createdByUserId,
        DateTime createdAt,
        DateTime? lastModifiedAt) =>
        new(publicId, title, content, renderedContent, type, scope, scopeEntityId,
            visibleFrom, visibleUntil, isDismissible, sortOrder, createdByUserId,
            createdAt, lastModifiedAt);

    public void Update(
        string title,
        string content,
        string renderedContent,
        AnnouncementTypeEnum type,
        DateTime? visibleFrom,
        DateTime? visibleUntil,
        bool isDismissible,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Announcement title cannot be empty", nameof(title));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Announcement content cannot be empty", nameof(content));

        if (visibleFrom.HasValue && visibleUntil.HasValue && visibleFrom >= visibleUntil)
            throw new ArgumentException("VisibleFrom must be before VisibleUntil");

        Title = title;
        Content = content;
        RenderedContent = renderedContent;
        Type = type;
        VisibleFrom = visibleFrom;
        VisibleUntil = visibleUntil;
        IsDismissible = isDismissible;
        SortOrder = sortOrder;
        LastModifiedAt = DateTime.UtcNow;
    }

    public bool IsCurrentlyVisible()
    {
        var now = DateTime.UtcNow;

        if (VisibleFrom.HasValue && now < VisibleFrom.Value)
            return false;

        if (VisibleUntil.HasValue && now > VisibleUntil.Value)
            return false;

        return true;
    }
}
