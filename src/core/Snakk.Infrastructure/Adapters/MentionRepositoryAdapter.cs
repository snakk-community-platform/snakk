namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Mappers;

public class MentionRepositoryAdapter(
    IMentionDatabaseRepository databaseRepository,
    SnakkDbContext context) : IMentionRepository
{
    public async Task<IEnumerable<Mention>> GetByPostIdAsync(PostId postId, CancellationToken ct = default)
    {
        var projections = await context.PostMentions
            .Where(m => m.PostPublicId == postId.Value)
            .Select(m => new MentionProjection(
                m.PublicId, m.PostPublicId!, m.MentionedUserPublicId!, m.CreatedAt))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddRangeAsync(IEnumerable<Mention> mentions, CancellationToken ct = default)
    {
        var mentionList = mentions.ToList();
        if (mentionList.Count == 0) return;

        // Batch-load all referenced posts and users in 2 queries instead of 2*N
        var postPublicIds = mentionList.Select(m => m.PostId.Value).Distinct().ToList();
        var userPublicIds = mentionList.Select(m => m.MentionedUserId.Value).Distinct().ToList();

        var posts = await context.Posts
            .Where(p => postPublicIds.Contains(p.PublicId))
            .Select(p => new { p.PublicId, p.Id })
            .ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);

        var users = await context.Users
            .Where(u => userPublicIds.Contains(u.PublicId))
            .Select(u => new { u.PublicId, u.Id })
            .ToDictionaryAsync(u => u.PublicId, u => u.Id, ct);

        var entities = new List<PostMentionDatabaseEntity>();

        foreach (var mention in mentionList)
        {
            if (!posts.TryGetValue(mention.PostId.Value, out var postId)) continue;
            if (!users.TryGetValue(mention.MentionedUserId.Value, out var userId)) continue;

            var entity = mention.ToPersistence();
            entity.PostId = postId;
            entity.PostPublicId = mention.PostId.Value;
            entity.MentionedUserId = userId;
            entity.MentionedUserPublicId = mention.MentionedUserId.Value;
            entities.Add(entity);
        }

        if (entities.Count > 0)
        {
            await databaseRepository.AddRangeAsync(entities, ct);
            await databaseRepository.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteByPostIdAsync(PostId postId, CancellationToken ct = default)
    {
        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value, ct);

        if (post is null) return;

        await databaseRepository.DeleteByPostIdAsync(post.Id, ct);
    }

    private record MentionProjection(
        string PublicId,
        string PostPublicId,
        string MentionedUserPublicId,
        DateTime CreatedAt)
    {
        public Mention ToDomain() => Mention.Rehydrate(
            MentionId.From(PublicId),
            PostId.From(PostPublicId),
            UserId.From(MentionedUserPublicId),
            CreatedAt);
    }
}
