namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

public class ReactionRepositoryAdapter(
    IReactionDatabaseRepository databaseRepository,
    SnakkDbContext context) : IReactionRepository
{
    public async Task<Reaction?> GetByUserPostAndTypeAsync(UserId userId, PostId postId, ReactionType type, CancellationToken ct = default)
    {
        var typeId = (int)type.ToShared();
        var projection = await context.PostReactions
            .Where(r =>
                r.UserPublicId == userId.Value
                && r.PostPublicId == postId.Value
                && r.TypeId == typeId)
            .Select(r => new ReactionProjection(
                r.PublicId, r.PostPublicId!, r.UserPublicId!,
                r.TypeId, r.CreatedAt))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Reaction?> GetByUserAndPostAsync(UserId userId, PostId postId, CancellationToken ct = default)
    {
        var projection = await context.PostReactions
            .Where(r =>
                r.UserPublicId == userId.Value
                && r.PostPublicId == postId.Value)
            .Select(r => new ReactionProjection(
                r.PublicId, r.PostPublicId!, r.UserPublicId!,
                r.TypeId, r.CreatedAt))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<Reaction>> GetByPostIdAsync(PostId postId, CancellationToken ct = default)
    {
        var projections = await context.PostReactions
            .Where(r => r.PostPublicId == postId.Value)
            .Select(r => new ReactionProjection(
                r.PublicId, r.PostPublicId!, r.UserPublicId!,
                r.TypeId, r.CreatedAt))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<Dictionary<ReactionType, int>> GetCountsByPostIdAsync(PostId postId, CancellationToken ct = default)
    {
        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value, ct);

        if (post is null) return new Dictionary<ReactionType, int>();

        var counts = await databaseRepository.GetCountsByPostIdAsync(post.Id, ct);

        return counts.ToDictionary(
            kvp => ((ReactionTypeEnum)kvp.Key).ToDomain(),
            kvp => kvp.Value);
    }

    public async Task<List<ReactionType>> GetUserReactionsForPostAsync(UserId userId, PostId postId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);
        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value, ct);

        if (user is null || post is null) return [];

        var typeIds = await databaseRepository.GetUserReactionTypesForPostAsync(user.Id, post.Id, ct);

        return typeIds
            .Select(id => ((ReactionTypeEnum)id).ToDomain())
            .ToList();
    }

    public async Task AddAsync(Reaction reaction, CancellationToken ct = default)
    {
        var entity = reaction.ToPersistence();

        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == reaction.PostId.Value, ct);
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == reaction.UserId.Value, ct);

        if (post is null)
            throw new InvalidOperationException($"Post with PublicId '{reaction.PostId}' not found");

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{reaction.UserId}' not found");

        entity.PostId = post.Id;
        entity.PostPublicId = post.PublicId;
        entity.UserId = user.Id;
        entity.UserPublicId = user.PublicId;

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Reaction reaction, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == reaction.UserId.Value, ct);
        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == reaction.PostId.Value, ct);

        if (user is null || post is null) return;

        var typeId = (int)reaction.Type.ToShared();
        var entity = await databaseRepository.GetByUserPostAndTypeAsync(user.Id, post.Id, typeId, ct);

        if (entity is null) return;

        await databaseRepository.DeleteAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, Dictionary<ReactionType, int>>> GetCountsByPostIdsAsync(IEnumerable<PostId> postIds, CancellationToken ct = default)
    {
        var publicIds = postIds
            .Select(p => p.Value)
            .ToList();

        var posts = await context.Posts
            .Where(p => publicIds.Contains(p.PublicId))
            .Select(p => new {
                p.Id,
                p.PublicId })
            .ToListAsync(ct);

        var postIdMap = posts.ToDictionary(p => p.Id, p => p.PublicId);
        var internalIds = posts
            .Select(p => p.Id)
            .ToList();

        var reactions = await context.PostReactions
            .Where(r => internalIds.Contains(r.PostId))
            .GroupBy(r => new {
                r.PostId,
                r.TypeId })
            .Select(g => new {
                g.Key.PostId,
                g.Key.TypeId,
                Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<string, Dictionary<ReactionType, int>>();

        // Initialize all posts with empty dictionaries
        foreach (var publicId in publicIds)
        {
            result[publicId] = new Dictionary<ReactionType, int>();
        }

        // Fill in the counts
        foreach (var r in reactions)
        {
            if (postIdMap.TryGetValue(r.PostId, out var publicId))
            {
                result[publicId][((ReactionTypeEnum)r.TypeId).ToDomain()] = r.Count;
            }
        }

        return result;
    }

    public async Task<Dictionary<string, List<ReactionType>>> GetUserReactionsForPostsAsync(
        UserId userId,
        IEnumerable<PostId> postIds,
        CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);

        if (user is null) return new Dictionary<string, List<ReactionType>>();

        var publicIds = postIds
            .Select(p => p.Value)
            .ToList();

        var posts = await context.Posts
            .Where(p => publicIds.Contains(p.PublicId))
            .Select(p => new {
                p.Id,
                p.PublicId })
            .ToListAsync(ct);

        var postIdMap = posts.ToDictionary(p => p.Id, p => p.PublicId);
        var internalIds = posts
            .Select(p => p.Id)
            .ToList();

        var userReactions = await context.PostReactions
            .Where(r =>
                r.UserId == user.Id
                && internalIds.Contains(r.PostId))
            .Select(r => new {
                r.PostId,
                r.TypeId })
            .ToListAsync(ct);

        var result = new Dictionary<string, List<ReactionType>>();

        foreach (var r in userReactions)
        {
            if (postIdMap.TryGetValue(r.PostId, out var publicId))
            {
                if (!result.ContainsKey(publicId))
                    result[publicId] = [];

                result[publicId].Add(((ReactionTypeEnum)r.TypeId).ToDomain());
            }
        }

        return result;
    }

    public async Task<ReactionBatchData> GetReactionDataAsync(IEnumerable<PostId> postIds, UserId? userId, CancellationToken ct = default)
    {
        var publicIds = postIds.Select(p => p.Value).ToList();

        var posts = await context.Posts
            .Where(p => publicIds.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId })
            .ToListAsync(ct);

        var postIdMap = posts.ToDictionary(p => p.Id, p => p.PublicId);
        var internalIds = posts.Select(p => p.Id).ToList();

        var reactions = await context.PostReactions
            .Where(r => internalIds.Contains(r.PostId))
            .GroupBy(r => new { r.PostId, r.TypeId })
            .Select(g => new { g.Key.PostId, g.Key.TypeId, Count = g.Count() })
            .ToListAsync(ct);

        var counts = new Dictionary<string, Dictionary<ReactionType, int>>();
        foreach (var publicId in publicIds)
            counts[publicId] = new Dictionary<ReactionType, int>();
        foreach (var r in reactions)
            if (postIdMap.TryGetValue(r.PostId, out var countPublicId))
                counts[countPublicId][((ReactionTypeEnum)r.TypeId).ToDomain()] = r.Count;

        var userReactions = new Dictionary<string, List<ReactionType>>();
        if (userId is not null)
        {
            var userDbId = await context.Users
                .Where(u => u.PublicId == userId.Value)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(ct);

            if (userDbId != 0)
            {
                var rawUserReactions = await context.PostReactions
                    .Where(r => r.UserId == userDbId && internalIds.Contains(r.PostId))
                    .Select(r => new { r.PostId, r.TypeId })
                    .ToListAsync(ct);

                foreach (var r in rawUserReactions)
                {
                    if (postIdMap.TryGetValue(r.PostId, out var urPublicId))
                    {
                        if (!userReactions.ContainsKey(urPublicId))
                            userReactions[urPublicId] = [];
                        userReactions[urPublicId].Add(((ReactionTypeEnum)r.TypeId).ToDomain());
                    }
                }
            }
        }

        return new ReactionBatchData(counts, userReactions);
    }

    public async Task<int> GetTotalReactionsReceivedByUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await context.PostReactions
            .Where(r => r.Post.CreatedByUserPublicId == userId.Value)
            .CountAsync(ct);
    }

    private record ReactionProjection(
        string PublicId,
        string PostPublicId,
        string UserPublicId,
        int TypeId,
        DateTime CreatedAt)
    {
        public Reaction ToDomain() => Reaction.Rehydrate(
            ReactionId.From(PublicId),
            PostId.From(PostPublicId),
            UserId.From(UserPublicId),
            ((ReactionTypeEnum)TypeId).ToDomain(),
            CreatedAt);
    }
}
