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
    private readonly IReactionDatabaseRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Reaction?> GetByUserAndPostAsync(UserId userId, PostId postId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value);

        if (user == null || post == null) return null;

        var entity = await _databaseRepository.GetByUserAndPostAsync(user.Id, post.Id);
        if (entity == null) return null;

        // Load navigation properties for mapping
        entity.User = user;
        entity.Post = post;

        return entity.FromPersistence();
    }

    public async Task<IEnumerable<Reaction>> GetByPostIdAsync(PostId postId)
    {
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value);
        if (post == null) return [];

        var entities = await _databaseRepository.GetByPostIdAsync(post.Id);
        return entities.Select(e => e.FromPersistence());
    }

    public async Task<Dictionary<ReactionType, int>> GetCountsByPostIdAsync(PostId postId)
    {
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value);
        if (post == null) return new Dictionary<ReactionType, int>();

        var counts = await _databaseRepository.GetCountsByPostIdAsync(post.Id);
        return counts.ToDictionary(
            kvp => ((ReactionTypeEnum)kvp.Key).ToDomain(),
            kvp => kvp.Value);
    }

    public async Task<ReactionType?> GetUserReactionForPostAsync(UserId userId, PostId postId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == postId.Value);

        if (user == null || post == null) return null;

        var typeId = await _databaseRepository.GetUserReactionTypeForPostAsync(user.Id, post.Id);
        if (typeId == null) return null;

        return ((ReactionTypeEnum)typeId.Value).ToDomain();
    }

    public async Task AddAsync(Reaction reaction)
    {
        var entity = reaction.ToPersistence();

        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == reaction.PostId.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == reaction.UserId.Value);

        if (post == null)
            throw new InvalidOperationException($"Post with PublicId '{reaction.PostId}' not found");
        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{reaction.UserId}' not found");

        entity.PostId = post.Id;
        entity.UserId = user.Id;

        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Reaction reaction)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == reaction.UserId.Value);
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.PublicId == reaction.PostId.Value);

        if (user == null || post == null) return;

        var entity = await _databaseRepository.GetByUserAndPostAsync(user.Id, post.Id);
        if (entity == null) return;

        await _databaseRepository.DeleteAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task<Dictionary<string, Dictionary<ReactionType, int>>> GetCountsByPostIdsAsync(IEnumerable<PostId> postIds)
    {
        var publicIds = postIds.Select(p => p.Value).ToList();
        var posts = await _context.Posts
            .Where(p => publicIds.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId })
            .ToListAsync();

        var postIdMap = posts.ToDictionary(p => p.Id, p => p.PublicId);
        var internalIds = posts.Select(p => p.Id).ToList();

        var reactions = await _context.Reactions
            .Where(r => internalIds.Contains(r.PostId))
            .GroupBy(r => new { r.PostId, r.TypeId })
            .Select(g => new { g.Key.PostId, g.Key.TypeId, Count = g.Count() })
            .ToListAsync();

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

    public async Task<Dictionary<string, ReactionType>> GetUserReactionsForPostsAsync(UserId userId, IEnumerable<PostId> postIds)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return new Dictionary<string, ReactionType>();

        var publicIds = postIds.Select(p => p.Value).ToList();
        var posts = await _context.Posts
            .Where(p => publicIds.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId })
            .ToListAsync();

        var postIdMap = posts.ToDictionary(p => p.Id, p => p.PublicId);
        var internalIds = posts.Select(p => p.Id).ToList();

        var userReactions = await _context.Reactions
            .Where(r => r.UserId == user.Id && internalIds.Contains(r.PostId))
            .Select(r => new { r.PostId, r.TypeId })
            .ToListAsync();

        var result = new Dictionary<string, ReactionType>();
        foreach (var r in userReactions)
        {
            if (postIdMap.TryGetValue(r.PostId, out var publicId))
            {
                result[publicId] = ((ReactionTypeEnum)r.TypeId).ToDomain();
            }
        }

        return result;
    }
}
