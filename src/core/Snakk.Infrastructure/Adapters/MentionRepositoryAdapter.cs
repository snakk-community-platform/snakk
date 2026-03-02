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
        var entities = new List<MentionDatabaseEntity>();

        foreach (var mention in mentions)
        {
            var entity = mention.ToPersistence();

            var post = await context.Posts.FirstOrDefaultAsync(p => p.PublicId == mention.PostId.Value);
            var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == mention.MentionedUserId.Value);

            if (post is null || user is null) continue;

            entity.PostId = post.Id;
            entity.MentionedUserId = user.Id;
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
