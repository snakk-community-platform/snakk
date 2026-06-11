namespace Snakk.Infrastructure.Adapters;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Extensions;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Models;

public class PostRepositoryAdapter(
    Infrastructure.Database.Repositories.IPostRepository databaseRepository,
    SnakkDbContext context,
    IConfiguration configuration) : Domain.Repositories.IPostRepository
{
    public async Task<Post?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var projection = await context.Posts
            .Where(p => p.Id == id)
            .Select(p => new PostProjection(
                p.PublicId, p.DiscussionPublicId, p.CreatedByUserPublicId,
                p.Content, p.RenderedContent, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion, p.IsUsersFirstPostInSpace, p.IsOp, p.IsNecro, p.IsMilestone,
                p.RevisionCount, p.WasNormalized))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Post?> GetByPublicIdAsync(PostId publicId, CancellationToken ct = default)
    {
        var projection = await context.Posts
            .Where(p => p.PublicId == publicId.Value)
            .Select(p => new PostProjection(
                p.PublicId, p.DiscussionPublicId, p.CreatedByUserPublicId,
                p.Content, p.RenderedContent, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion, p.IsUsersFirstPostInSpace, p.IsOp, p.IsNecro, p.IsMilestone,
                p.RevisionCount, p.WasNormalized))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<Post>> GetByPublicIdsAsync(IEnumerable<PostId> publicIds, CancellationToken ct = default)
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
                p.DiscussionPublicId,
                p.CreatedByUserPublicId,
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
                p.RevisionCount,
                p.WasNormalized))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<IEnumerable<Post>> GetByDiscussionIdAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        var dbId = await context.Discussions.Where(d => d.PublicId == discussionId.Value).Select(d => d.Id).FirstOrDefaultAsync(ct);
        var projections = await context.Posts
            .Where(p => p.DiscussionId == dbId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PostProjection(
                p.PublicId, p.DiscussionPublicId, p.CreatedByUserPublicId,
                p.Content, p.RenderedContent, p.CreatedAt, p.LastModifiedAt, p.EditedAt, p.IsFirstPost,
                p.ReplyToPost != null ? p.ReplyToPost.PublicId : null,
                p.IsDeleted, p.HasCodeBlock,
                p.IsUsersFirstPostInDiscussion, p.IsUsersFirstPostInSpace, p.IsOp, p.IsNecro, p.IsMilestone,
                p.RevisionCount, p.WasNormalized))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<PagedResult<Post>> GetPagedByDiscussionIdAsync(
        DiscussionId discussionId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        var discussionDbId = await context.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => d.Id)
            .FirstOrDefaultAsync(ct);

        if (discussionDbId == 0)
            return new PagedResult<Post>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };

        var result = await context.Posts
            .Where(p => p.DiscussionId == discussionDbId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PostProjection(
                p.PublicId,
                p.DiscussionPublicId,
                p.CreatedByUserPublicId,
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
                p.RevisionCount,
                p.WasNormalized))
            .ToPagedResultAsync(offset, pageSize, ct);

        return result.Map(p => p.ToDomain());
    }

    public async Task AddAsync(Post post, CancellationToken ct = default)
    {
        var entity = post.ToPersistence();

        // Resolve foreign keys (sequential — EF Core DbContext is not thread-safe)
        var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == post.DiscussionId.Value, ct)
            ?? throw new InvalidOperationException($"Discussion with PublicId '{post.DiscussionId}' not found");

        if (post.CreatedByUserId is null)
            throw new InvalidOperationException("Cannot save a post with no author");

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == post.CreatedByUserId.Value, ct)
            ?? throw new InvalidOperationException($"User with PublicId '{post.CreatedByUserId}' not found");

        entity.DiscussionId = discussion.Id;
        entity.DiscussionPublicId = discussion.PublicId;
        entity.SpaceId = discussion.SpaceId;
        entity.SpacePublicId = discussion.SpacePublicId;
        entity.HubId = discussion.HubId;
        entity.HubPublicId = discussion.HubPublicId;
        entity.CommunityId = discussion.CommunityId;
        entity.CommunityPublicId = discussion.CommunityPublicId;
        entity.CreatedByUserId = user.Id;
        entity.CreatedByUserPublicId = user.PublicId;

        if (post.ReplyToPostId is not null)
        {
            entity.ReplyToPostId = await context.Posts
                .Where(p => p.PublicId == post.ReplyToPostId.Value)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        entity.IsOp = discussion.CreatedByUserId == user.Id;

        // Compute denormalized post flags (sequential — same DbContext)
        entity.IsUsersFirstPostInDiscussion = !await context.Posts
            .AnyAsync(p => p.DiscussionId == discussion.Id && p.CreatedByUserId == user.Id, ct);

        entity.IsUsersFirstPostInSpace = !await context.Posts
            .AnyAsync(p => p.SpaceId == discussion.SpaceId && p.CreatedByUserId == user.Id, ct);

        var necroDays = configuration.GetValue("PostFlags:NecroDays", 30);
        var lastPostDate = await context.Posts
            .Where(p => p.DiscussionId == discussion.Id && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (DateTime?)p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        entity.IsNecro = lastPostDate.HasValue
            && (DateTime.UtcNow - lastPostDate.Value).TotalDays >= necroDays;

        var postCount = await context.Posts
            .Where(p => p.DiscussionId == discussion.Id)
            .CountAsync(ct);

        var milestoneThresholds = configuration
            .GetSection("PostFlags:MilestoneThresholds")
            .Get<int[]>() ?? [100, 500, 1000, 2500, 5000, 10000, 20000, 30000, 40000, 50000];

        entity.IsMilestone = milestoneThresholds.Contains(postCount + 1);
        entity.PlainTextExcerpt = StripMarkdown(post.Content);

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Post post, CancellationToken ct = default)
    {
        var entity = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == post.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Post with PublicId '{post.PublicId}' not found");

        entity.Content = post.Content;
        entity.RenderedContent = post.RenderedContent;
        entity.PlainTextExcerpt = StripMarkdown(post.Content);
        entity.LastModifiedAt = post.LastModifiedAt;
        entity.EditedAt = post.EditedAt;
        entity.IsDeleted = post.IsDeleted;
        entity.RevisionCount = post.RevisionCount;
        entity.WasNormalized = post.WasNormalized;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Post post, CancellationToken ct = default)
    {
        var entity = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == post.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Post with PublicId '{post.PublicId}' not found");

        context.Posts.Remove(entity);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddRevisionAsync(PostRevision revision, CancellationToken ct = default)
    {
        var entity = revision.ToPersistence();

        // Resolve foreign keys
        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == revision.PostId.Value, ct);

        if (post is null)
            throw new InvalidOperationException($"Post with PublicId '{revision.PostId}' not found");

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == revision.EditedByUserId.Value, ct);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{revision.EditedByUserId}' not found");

        entity.PostId = post.Id;
        entity.EditedByUserId = user.Id;

        context.PostRevisions.Add(entity);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<PostRevision>> GetRevisionsAsync(PostId postId, CancellationToken ct = default)
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
            .ToListAsync(ct);

        return revisions.Select(r => PostRevision.Rehydrate(
            PostId.From(r.PostPublicId),
            r.Content,
            UserId.From(r.EditedByUserPublicId),
            r.RevisionNumber,
            r.CreatedAt));
    }

    public async Task<int> GetPostNumberInDiscussionAsync(DiscussionId discussionId, DateTime createdAt, CancellationToken ct = default)
    {
        var discussionDbEntity = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == discussionId.Value, ct);

        if (discussionDbEntity is null)
            return 0;

        return await context.Posts
            .Where(p =>
                p.DiscussionId == discussionDbEntity.Id
                && !p.IsDeleted
                && p.CreatedAt <= createdAt)
            .CountAsync(ct);
    }

    public async Task<Post?> GetFirstPostByDiscussionIdAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        var discussionDbEntity = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == discussionId.Value, ct);

        if (discussionDbEntity is null)
            return null;

        var projection = await context.Posts
            .Where(p =>
                p.DiscussionId == discussionDbEntity.Id
                && p.IsFirstPost)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PostProjection(
                p.PublicId,
                p.DiscussionPublicId,
                p.CreatedByUserPublicId,
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
                p.RevisionCount,
                p.WasNormalized))
            .FirstOrDefaultAsync(ct);

        return projection?.ToDomain();
    }

    public async Task<List<(UserId UserId, int PostCount)>> GetTopContributorsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit,
        CancellationToken ct = default)
    {
        var postsQuery = context.Posts
            .Where(p => p.CreatedAt >= since);

        // Resolve public IDs to internal IDs once, outside the query
        if (communityId is not null)
        {
            var dbId = await context.Communities.Where(c => c.PublicId == communityId.Value).Select(c => c.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.CommunityId == dbId);
        }

        if (hubId is not null)
        {
            var dbId = await context.Hubs.Where(h => h.PublicId == hubId.Value).Select(h => h.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.HubId == dbId);
        }

        if (spaceId is not null)
        {
            var dbId = await context.Spaces.Where(s => s.PublicId == spaceId.Value).Select(s => s.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.SpaceId == dbId);
        }

        var topContributors = await postsQuery
            .GroupBy(p => p.CreatedByUserPublicId)
            .Select(g => new { PublicId = g.Key, PostCount = g.Count() })
            .OrderByDescending(x => x.PostCount)
            .Take(limit)
            .ToListAsync(ct);

        return topContributors
            .Where(c => c.PublicId is not null)
            .Select(c => (UserId.From(c.PublicId!), c.PostCount))
            .ToList();
    }

    public async Task<List<(UserId UserId, DateTime LastPostAt)>> GetLatestContributorsAsync(
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit,
        CancellationToken ct = default)
    {
        // No scope filter — query User.LastSeenAt directly instead of GROUP BY across all posts.
        if (communityId is null && hubId is null && spaceId is null)
        {
            var recent = await context.Users
                .Where(u => !u.IsDeleted && u.LastSeenAt != null)
                .OrderByDescending(u => u.LastSeenAt)
                .Take(limit)
                .Select(u => new { u.PublicId, LastSeenAt = u.LastSeenAt!.Value })
                .ToListAsync(ct);

            return recent.Select(u => (UserId.From(u.PublicId), u.LastSeenAt)).ToList();
        }

        var postsQuery = context.Posts.AsQueryable();

        if (communityId is not null)
        {
            var dbId = await context.Communities.Where(c => c.PublicId == communityId.Value).Select(c => c.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.CommunityId == dbId);
        }

        if (hubId is not null)
        {
            var dbId = await context.Hubs.Where(h => h.PublicId == hubId.Value).Select(h => h.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.HubId == dbId);
        }

        if (spaceId is not null)
        {
            var dbId = await context.Spaces.Where(s => s.PublicId == spaceId.Value).Select(s => s.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.SpaceId == dbId);
        }

        var latestContributors = await postsQuery
            .GroupBy(p => p.CreatedByUserPublicId)
            .Select(g => new { UserId = g.Key, LastPostAt = g.Max(p => p.CreatedAt) })
            .OrderByDescending(x => x.LastPostAt)
            .Take(limit)
            .ToListAsync(ct);

        return latestContributors
            .Where(c => c.UserId is not null)
            .Select(c => (UserId.From(c.UserId!), c.LastPostAt))
            .ToList();
    }

    public async Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(
        UserId userId,
        DateTime startDate,
        CancellationToken ct = default)
    {
        // Get the internal user ID
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

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
            .ToListAsync(ct);

        return activity.Select(a => (a.Date, a.Count));
    }

    public async Task<List<Domain.Repositories.TopSpaceForUser>> GetTopSpacesForUserAsync(UserId userId, int limit, CancellationToken ct = default)
    {
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userDbId == 0)
            return [];

        var topSpaceData = await context.Posts
            .Where(p => p.CreatedByUserId == userDbId)
            .GroupBy(p => p.SpaceId)
            .Select(g => new { SpaceId = g.Key, PostCount = g.Count() })
            .OrderByDescending(x => x.PostCount)
            .Take(limit)
            .ToListAsync(ct);

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
                HubSlug = s.HubSlug,
                CommunitySlug = s.CommunitySlug
            })
            .ToListAsync(ct);

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

    private static string? StripMarkdown(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var s = content;
        s = Regex.Replace(s, @"!\[([^\]]*)\]\([^)]*\)", "");             // images → removed
        s = Regex.Replace(s, @"\[([^\]]*)\]\([^)]*\)", "$1");            // links
        s = Regex.Replace(s, @"```[\s\S]*?```", "", RegexOptions.Singleline); // code blocks
        s = Regex.Replace(s, @"`([^`]+)`", "$1");                        // inline code
        s = Regex.Replace(s, @"\*{1,3}([^*\n]+)\*{1,3}", "$1");         // bold/italic
        s = Regex.Replace(s, @"_{1,3}([^_\n]+)_{1,3}", "$1");           // underscore bold/italic
        s = Regex.Replace(s, @"^#{1,6}\s+", "", RegexOptions.Multiline); // headings
        s = Regex.Replace(s, @"^>\s*", "", RegexOptions.Multiline);      // blockquotes
        s = Regex.Replace(s, @"^[-*_]{3,}\s*$", "", RegexOptions.Multiline); // hr
        s = Regex.Replace(s, @"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$", "", RegexOptions.Multiline); // table separator row
        s = Regex.Replace(s, @"\|", " ");                                    // pipe characters in tables
        s = Regex.Replace(s, @"\s+", " ").Trim();
        if (s.Length == 0) return null;
        return s.Length > 200 ? s[..200] : s;
    }

    private record PostProjection(
        string PublicId,
        string DiscussionPublicId,
        string? CreatedByUserPublicId,
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
        int RevisionCount,
        bool WasNormalized)
    {
        public Post ToDomain() => Post.Rehydrate(
            PostId.From(PublicId),
            DiscussionId.From(DiscussionPublicId),
            UserId.FromNullable(CreatedByUserPublicId),
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
            RevisionCount,
            wasNormalized: WasNormalized);
    }
}
