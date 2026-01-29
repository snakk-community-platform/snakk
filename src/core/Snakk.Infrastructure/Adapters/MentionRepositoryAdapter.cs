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
    private readonly IMentionDatabaseRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<IEnumerable<Mention>> GetByPostIdAsync(PostId postId)
    {
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value);
        if (post == null) return [];

        var entities = await _databaseRepository.GetByPostIdAsync(post.Id);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task AddRangeAsync(IEnumerable<Mention> mentions)
    {
        var entities = new List<MentionDatabaseEntity>();

        foreach (var mention in mentions)
        {
            var entity = mention.ToPersistence();

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == mention.PostId.Value);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == mention.MentionedUserId.Value);

            if (post == null || user == null) continue;

            entity.PostId = post.Id;
            entity.MentionedUserId = user.Id;
            entities.Add(entity);
        }

        if (entities.Count > 0)
        {
            await _databaseRepository.AddRangeAsync(entities);
            await _databaseRepository.SaveChangesAsync();
        }
    }

    public async Task DeleteByPostIdAsync(PostId postId)
    {
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value);
        if (post == null) return;

        await _databaseRepository.DeleteByPostIdAsync(post.Id);
    }
}
