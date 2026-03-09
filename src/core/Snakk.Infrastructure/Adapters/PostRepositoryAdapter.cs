namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class PostRepositoryAdapter(
    Infrastructure.Database.Repositories.IPostRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IPostRepository
{
    public async Task<Post?> GetByIdAsync(int id)
    {
        var projection = await context.Posts
            .Where(p => p.Id == id)
            .Select(p => new PostProjection(
                p.PublicId, p.Discussion.PublicId, p.CreatedByUser.PublicId,
                p.Content, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock, p.RevisionCount))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Post?> GetByPublicIdAsync(PostId publicId)
    {
        var projection = await context.Posts
            .Where(p => p.PublicId == publicId.Value)
            .Select(p => new PostProjection(
                p.PublicId, p.Discussion.PublicId, p.CreatedByUser.PublicId,
                p.Content, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock, p.RevisionCount))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<Post>> GetByPublicIdsAsync(IEnumerable<PostId> publicIds)
    {
        var publicIdStrings = publicIds
            .Select(id => id.Value)
            .ToList();

        if (publicIdStrings.Count == 0)
            return [];

        var projections = await context.Posts
            .Where(p => publicIdStrings.Contains(p.PublicId))
            .Select(p => new PostProjection(
                p.PublicId,
                p.Discussion.PublicId,
                p.CreatedByUser.PublicId,
                p.Content,
                p.CreatedAt,
                p.LastModifiedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted,
                p.HasCodeBlock,
                p.RevisionCount))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task<IEnumerable<Post>> GetByDiscussionIdAsync(DiscussionId discussionId)
    {
        var projections = await context.Posts
            .Where(p => p.Discussion.PublicId == discussionId.Value)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PostProjection(
                p.PublicId, p.Discussion.PublicId, p.CreatedByUser.PublicId,
                p.Content, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock, p.RevisionCount))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task<PagedResult<Post>> GetPagedByDiscussionIdAsync(
        DiscussionId discussionId,
        int offset,
        int pageSize)
    {
        var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussionId.Value);

        if (discussion is null)
            return new PagedResult<Post>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };

        var projections = await context.Posts
            .Where(p => p.DiscussionId == discussion.Id)
            .OrderBy(p => p.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(p => new PostProjection(
                p.PublicId,
                p.Discussion.PublicId,
                p.CreatedByUser.PublicId,
                p.Content,
                p.CreatedAt,
                p.LastModifiedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted,
                p.HasCodeBlock,
                p.RevisionCount))
            .ToListAsync();

        var hasMoreItems = projections.Count > pageSize;
        var resultItems = hasMoreItems
            ? projections
                .Take(pageSize)
                .Select(p => p.ToDomain())
            : projections.Select(p => p.ToDomain());

        return new PagedResult<Post>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task AddAsync(Post post)
    {
        var entity = post.ToPersistence();

        var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == post.DiscussionId.Value);

        if (discussion is null)
            throw new InvalidOperationException($"Discussion with PublicId '{post.DiscussionId}' not found");

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == post.CreatedByUserId.Value);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{post.CreatedByUserId}' not found");

        entity.DiscussionId = discussion.Id;
        entity.CreatedByUserId = user.Id;

        if (post.ReplyToPostId is not null)
        {
            var replyToPost = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == post.ReplyToPostId.Value);

            if (replyToPost is not null)
            {
                entity.ReplyToPostId = replyToPost.Id;
            }
        }

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Post post)
    {
        var entity = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == post.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Post with PublicId '{post.PublicId}' not found");

        entity.Content = post.Content;
        entity.LastModifiedAt = post.LastModifiedAt;
        entity.EditedAt = post.EditedAt;
        entity.IsDeleted = post.IsDeleted;
        entity.RevisionCount = post.RevisionCount;

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Post post)
    {
        var entity = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == post.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Post with PublicId '{post.PublicId}' not found");

        context.Posts.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task AddRevisionAsync(PostRevision revision)
    {
        var entity = revision.ToPersistence();

        // Resolve foreign keys
        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == revision.PostId.Value);

        if (post is null)
            throw new InvalidOperationException($"Post with PublicId '{revision.PostId}' not found");

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == revision.EditedByUserId.Value);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{revision.EditedByUserId}' not found");

        entity.PostId = post.Id;
        entity.EditedByUserId = user.Id;

        context.PostRevisions.Add(entity);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<PostRevision>> GetRevisionsAsync(PostId postId)
    {
        var revisions = await context.PostRevisions
            .Where(pr => pr.PostPublicId == postId.Value)
            .OrderByDescending(pr => pr.RevisionNumber)
            .Select(pr => new {
                pr.PostPublicId,
                pr.Content,
                pr.EditedByUserPublicId,
                pr.RevisionNumber,
                pr.CreatedAt })
            .ToListAsync();

        return revisions.Select(r => PostRevision.Rehydrate(
            PostId.From(r.PostPublicId),
            r.Content,
            UserId.From(r.EditedByUserPublicId),
            r.RevisionNumber,
            r.CreatedAt));
    }

    public async Task<int> GetPostNumberInDiscussionAsync(DiscussionId discussionId, DateTime createdAt)
    {
        var discussionDbEntity = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == discussionId.Value);

        if (discussionDbEntity is null)
            return 0;

        return await context.Posts
            .Where(p =>
                p.DiscussionId == discussionDbEntity.Id
                && !p.IsDeleted
                && p.CreatedAt <= createdAt)
            .CountAsync();
    }

    public async Task<Post?> GetFirstPostByDiscussionIdAsync(DiscussionId discussionId)
    {
        var discussionDbEntity = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == discussionId.Value);

        if (discussionDbEntity is null)
            return null;

        var projection = await context.Posts
            .Where(p =>
                p.DiscussionId == discussionDbEntity.Id
                && p.IsFirstPost)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PostProjection(
                p.PublicId,
                p.Discussion.PublicId,
                p.CreatedByUser.PublicId,
                p.Content,
                p.CreatedAt,
                p.LastModifiedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted,
                p.HasCodeBlock,
                p.RevisionCount))
            .FirstOrDefaultAsync();

        return projection?.ToDomain();
    }

    public async Task<List<(UserId UserId, int PostCount)>> GetTopContributorsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit)
    {
        var postsQuery = context.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= since);

        // Apply filters based on hierarchy
        if (communityId is not null)
        {
            postsQuery = postsQuery.Where(p =>
                p.Discussion.Space.Hub.CommunityId == context.Communities
                    .Where(c => c.PublicId == communityId.Value)
                    .Select(c => c.Id)
                    .FirstOrDefault());
        }

        if (hubId is not null)
        {
            postsQuery = postsQuery.Where(p =>
                p.Discussion.Space.HubId == context.Hubs
                    .Where(h => h.PublicId == hubId.Value)
                    .Select(h => h.Id)
                    .FirstOrDefault());
        }

        if (spaceId is not null)
        {
            postsQuery = postsQuery.Where(p =>
                p.Discussion.SpaceId == context.Spaces
                    .Where(s => s.PublicId == spaceId.Value)
                    .Select(s => s.Id)
                    .FirstOrDefault());
        }

        var topContributors = await postsQuery
            .GroupBy(p => p.CreatedByUser.PublicId)
            .Select(g => new {
                UserId = g.Key,
                PostCount = g.Count() })
            .OrderByDescending(x => x.PostCount)
            .Take(limit)
            .ToListAsync();

        return topContributors
            .Select(c => (UserId.From(c.UserId), c.PostCount))
            .ToList();
    }

    public async Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(
        UserId userId,
        DateTime startDate)
    {
        // Get the internal user ID
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userDbId == 0)
            return [];

        var activity = await context.Posts
            .Where(p =>
                p.CreatedByUserId == userDbId
                && !p.IsFirstPost
                && p.CreatedAt >= startDate)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new {
                Date = g.Key,
                Count = g.Count() })
            .ToListAsync();

        return activity.Select(a => (a.Date, a.Count));
    }

    private record PostProjection(
        string PublicId,
        string DiscussionPublicId,
        string CreatedByUserPublicId,
        string Content,
        DateTime CreatedAt,
        DateTime? LastModifiedAt,
        DateTime? EditedAt,
        bool IsFirstPost,
        string? ReplyToPostPublicId,
        bool IsDeleted,
        bool HasCodeBlock,
        int RevisionCount)
    {
        public Post ToDomain() => Post.Rehydrate(
            PostId.From(PublicId),
            DiscussionId.From(DiscussionPublicId),
            UserId.From(CreatedByUserPublicId),
            Content,
            CreatedAt,
            LastModifiedAt,
            EditedAt,
            IsFirstPost,
            ReplyToPostPublicId is not null ? PostId.From(ReplyToPostPublicId) : null,
            IsDeleted,
            HasCodeBlock,
            RevisionCount);
    }
}
