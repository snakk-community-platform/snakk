namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class PostMapper
{
    public static Post FromPersistence(this PostDatabaseEntity entity) =>
        // Note: Revisions are loaded on demand, so they may not be available here
        Post.Rehydrate(
            PostId.From(entity.PublicId),
            DiscussionId.From(entity.Discussion.PublicId),
            UserId.From(entity.CreatedByUser.PublicId),
            entity.Content,
            entity.RenderedContent,
            entity.CreatedAt,
            entity.LastModifiedAt,
            entity.EditedAt,
            entity.IsFirstPost,
            entity.ReplyToPostId is not null ? PostId.From(entity.ReplyToPost!.PublicId) : null,
            entity.IsDeleted,
            entity.HasCodeBlock,
            entity.RevisionCount);

    public static PostDatabaseEntity ToPersistence(this Post post) =>
        // Note: Navigation properties must be set separately in repository adapter
        new()
        {
            PublicId = post.PublicId,
            Content = post.Content,
            RenderedContent = post.RenderedContent,
            CreatedAt = post.CreatedAt,
            LastModifiedAt = post.LastModifiedAt,
            EditedAt = post.EditedAt,
            IsFirstPost = post.IsFirstPost,
            IsDeleted = post.IsDeleted,
            HasCodeBlock = post.HasCodeBlock,
            RevisionCount = post.RevisionCount
            // DiscussionId, CreatedByUserId, ReplyToPostId will be set by repository adapter
        };
}
