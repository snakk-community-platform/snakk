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
    public async Task<IEnumerable<Mention>> GetByPostIdAsync(PostId postId)
    {
        var projections = await context.Mentions
            .Where(m => m.Post.PublicId == postId.Value)
            .Select(m => new MentionProjection(
                m.PublicId, m.Post.PublicId, m.MentionedUser.PublicId, m.CreatedAt))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task AddRangeAsync(IEnumerable<Mention> mentions)
    {
        var mentionList = mentions.ToList();
        if (mentionList.Count == 0) return;

        // Batch-load all referenced posts and users in 2 queries instead of 2*N
        var postPublicIds = mentionList.Select(m => m.PostId.Value).Distinct().ToList();
        var userPublicIds = mentionList.Select(m => m.MentionedUserId.Value).Distinct().ToList();

        var posts = await context.Posts
            .Where(p => postPublicIds.Contains(p.PublicId))
            .Select(p => new { p.PublicId, p.Id })
            .ToDictionaryAsync(p => p.PublicId, p => p.Id);

        var users = await context.Users
            .Where(u => userPublicIds.Contains(u.PublicId))
            .Select(u => new { u.PublicId, u.Id })
            .ToDictionaryAsync(u => u.PublicId, u => u.Id);

        var entities = new List<MentionDatabaseEntity>();

        foreach (var mention in mentionList)
        {
            if (!posts.TryGetValue(mention.PostId.Value, out var postId)) continue;
            if (!users.TryGetValue(mention.MentionedUserId.Value, out var userId)) continue;

            var entity = mention.ToPersistence();
            entity.PostId = postId;
            entity.MentionedUserId = userId;
            entities.Add(entity);
        }

        if (entities.Count > 0)
        {
            await databaseRepository.AddRangeAsync(entities);
            await databaseRepository.SaveChangesAsync();
        }
    }

    public async Task DeleteByPostIdAsync(PostId postId)
    {
        var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value);

        if (post is null) return;

        await databaseRepository.DeleteByPostIdAsync(post.Id);
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
