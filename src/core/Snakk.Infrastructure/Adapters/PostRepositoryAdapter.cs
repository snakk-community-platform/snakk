namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class PostRepositoryAdapter(
    Infrastructure.Database.Repositories.IPostRepository databaseRepository,
    SnakkDbContext context,
    IConfiguration configuration) : Domain.Repositories.IPostRepository
{
    public async Task<Post?> GetByIdAsync(int id)
    {
        var projection = await context.Posts
            .Where(p => p.Id == id)
            .Select(p => new PostProjection(
                p.PublicId, p.Discussion.PublicId, p.CreatedByUser.PublicId,
                p.Content, p.RenderedContent, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion, p.IsUsersFirstPostInSpace, p.IsOp, p.IsNecro, p.IsMilestone,
                p.RevisionCount))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Post?> GetByPublicIdAsync(PostId publicId)
    {
        var projection = await context.Posts
            .Where(p => p.PublicId == publicId.Value)
            .Select(p => new PostProjection(
                p.PublicId, p.Discussion.PublicId, p.CreatedByUser.PublicId,
                p.Content, p.RenderedContent, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion, p.IsUsersFirstPostInSpace, p.IsOp, p.IsNecro, p.IsMilestone,
                p.RevisionCount))
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
                p.RenderedContent,
                p.CreatedAt,
                p.LastModifiedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted,
                p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion,
                p.IsUsersFirstPostInSpace,
                p.IsOp,
                p.IsNecro,
                p.IsMilestone,
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
                p.Content, p.RenderedContent, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion, p.IsUsersFirstPostInSpace, p.IsOp, p.IsNecro, p.IsMilestone,
                p.RevisionCount))
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
                p.RenderedContent,
                p.CreatedAt,
                p.LastModifiedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted,
                p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion,
                p.IsUsersFirstPostInSpace,
                p.IsOp,
                p.IsNecro,
                p.IsMilestone,
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

        // Resolve foreign keys (sequential — EF Core DbContext is not thread-safe)
        var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == post.DiscussionId.Value)
            ?? throw new InvalidOperationException($"Discussion with PublicId '{post.DiscussionId}' not found");

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == post.CreatedByUserId.Value)
            ?? throw new InvalidOperationException($"User with PublicId '{post.CreatedByUserId}' not found");

        entity.DiscussionId = discussion.Id;
        entity.CreatedByUserId = user.Id;

        if (post.ReplyToPostId is not null)
        {
            entity.ReplyToPostId = await context.Posts
                .Where(p => p.PublicId == post.ReplyToPostId.Value)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();
        }

        entity.IsOp = discussion.CreatedByUserId == user.Id;

        // Compute denormalized post flags (sequential — same DbContext)
        entity.IsUsersFirstPostInDiscussion = !await context.Posts
            .AnyAsync(p => p.DiscussionId == discussion.Id && p.CreatedByUserId == user.Id);

        entity.IsUsersFirstPostInSpace = !await context.Posts
            .AnyAsync(p => p.Discussion.SpaceId == discussion.SpaceId && p.CreatedByUserId == user.Id);

        var necroDays = configuration.GetValue("PostFlags:NecroDays", 30);
        var lastPostDate = await context.Posts
            .Where(p => p.DiscussionId == discussion.Id && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (DateTime?)p.CreatedAt)
            .FirstOrDefaultAsync();

        entity.IsNecro = lastPostDate.HasValue
            && (DateTime.UtcNow - lastPostDate.Value).TotalDays >= necroDays;

        var postCount = await context.Posts
            .Where(p => p.DiscussionId == discussion.Id)
            .CountAsync();

        var milestoneThresholds = configuration
            .GetSection("PostFlags:MilestoneThresholds")
            .Get<int[]>() ?? [100, 500, 1000, 2500, 5000, 10000, 20000, 30000, 40000, 50000];

        entity.IsMilestone = milestoneThresholds.Contains(postCount + 1);

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Post post)
    {
        var entity = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == post.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"Post with PublicId '{post.PublicId}' not found");

        entity.Content = post.Content;
        entity.RenderedContent = post.RenderedContent;
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
                p.RenderedContent,
                p.CreatedAt,
                p.LastModifiedAt,
                p.EditedAt,
                p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted,
                p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion,
                p.IsUsersFirstPostInSpace,
                p.IsOp,
                p.IsNecro,
                p.IsMilestone,
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

        // Resolve public IDs to internal IDs once, outside the query
        if (communityId is not null)
        {
            var dbId = await context.Communities.Where(c => c.PublicId == communityId.Value).Select(c => c.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.Space.Hub.CommunityId == dbId);
        }

        if (hubId is not null)
        {
            var dbId = await context.Hubs.Where(h => h.PublicId == hubId.Value).Select(h => h.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.Space.HubId == dbId);
        }

        if (spaceId is not null)
        {
            var dbId = await context.Spaces.Where(s => s.PublicId == spaceId.Value).Select(s => s.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.SpaceId == dbId);
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

    public async Task<List<(UserId UserId, DateTime LastPostAt)>> GetLatestContributorsAsync(
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit)
    {
        var postsQuery = context.Posts.Where(p => !p.IsDeleted);

        if (communityId is not null)
        {
            var dbId = await context.Communities.Where(c => c.PublicId == communityId.Value).Select(c => c.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.Space.Hub.CommunityId == dbId);
        }

        if (hubId is not null)
        {
            var dbId = await context.Hubs.Where(h => h.PublicId == hubId.Value).Select(h => h.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.Space.HubId == dbId);
        }

        if (spaceId is not null)
        {
            var dbId = await context.Spaces.Where(s => s.PublicId == spaceId.Value).Select(s => s.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.SpaceId == dbId);
        }

        var latestContributors = await postsQuery
            .GroupBy(p => p.CreatedByUser.PublicId)
            .Select(g => new { UserId = g.Key, LastPostAt = g.Max(p => p.CreatedAt) })
            .OrderByDescending(x => x.LastPostAt)
            .Take(limit)
            .ToListAsync();

        return latestContributors
            .Select(c => (UserId.From(c.UserId), c.LastPostAt))
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

    public async Task<List<Domain.Repositories.TopSpaceForUser>> GetTopSpacesForUserAsync(UserId userId, int limit)
    {
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userDbId == 0)
            return [];

        var topSpaceData = await context.Posts
            .Where(p => p.CreatedByUserId == userDbId && !p.IsDeleted)
            .GroupBy(p => p.Discussion.SpaceId)
            .Select(g => new { SpaceId = g.Key, PostCount = g.Count() })
            .OrderByDescending(x => x.PostCount)
            .Take(limit)
            .ToListAsync();

        if (topSpaceData.Count == 0)
            return [];

        var spaceIds = topSpaceData.Select(x => x.SpaceId).ToList();

        var spaces = await context.Spaces
            .Where(s => spaceIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.PublicId,
                s.Slug,
                s.Name,
                s.AvatarFileName,
                HubSlug = s.Hub.Slug,
                CommunitySlug = s.Hub.Community.Slug
            })
            .ToListAsync();

        var spacesById = spaces.ToDictionary(s => s.Id);

        return topSpaceData
            .Where(t => spacesById.ContainsKey(t.SpaceId))
            .Select(t =>
            {
                var s = spacesById[t.SpaceId];
                return new Domain.Repositories.TopSpaceForUser(
                    s.PublicId,
                    s.Slug,
                    s.Name,
                    s.AvatarFileName,
                    s.HubSlug,
                    s.CommunitySlug,
                    t.PostCount);
            })
            .ToList();
    }

    private record PostProjection(
        string PublicId,
        string DiscussionPublicId,
        string CreatedByUserPublicId,
        string Content,
        string RenderedContent,
        DateTime CreatedAt,
        DateTime? LastModifiedAt,
        DateTime? EditedAt,
        bool IsFirstPost,
        string? ReplyToPostPublicId,
        bool IsDeleted,
        bool HasCodeBlock,
        bool IsUsersFirstPostInDiscussion,
        bool IsUsersFirstPostInSpace,
        bool IsOp,
        bool IsNecro,
        bool IsMilestone,
        int RevisionCount)
    {
        public Post ToDomain() => Post.Rehydrate(
            PostId.From(PublicId),
            DiscussionId.From(DiscussionPublicId),
            UserId.From(CreatedByUserPublicId),
            Content,
            RenderedContent,
            CreatedAt,
            LastModifiedAt,
            EditedAt,
            IsFirstPost,
            ReplyToPostPublicId is not null ? PostId.From(ReplyToPostPublicId) : null,
            IsDeleted,
            HasCodeBlock,
            IsUsersFirstPostInDiscussion,
            IsUsersFirstPostInSpace,
            IsOp,
            IsNecro,
            IsMilestone,
            RevisionCount);
    }
}
