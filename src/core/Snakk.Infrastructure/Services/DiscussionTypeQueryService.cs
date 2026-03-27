using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class DiscussionTypeQueryService(SnakkDbContext context, IConfiguration configuration) : IDiscussionTypeQueryService
{
    private readonly string _mediaUrlBase = configuration["FileStorage:MediaUrlBase"] ?? "/storage";
    // === Question ===

    public async Task<QuestionStatus?> GetQuestionStatusAsync(string discussionPublicId)
    {
        var question = await context.DiscussionQuestions
            .Where(q => q.Discussion.PublicId == discussionPublicId && !q.Discussion.IsDeleted)
            .Select(q => new
            {
                q.SolvedAt,
                AcceptedPostPublicId = q.AcceptedPost != null ? q.AcceptedPost.PublicId : null
            })
            .FirstOrDefaultAsync();

        if (question is null) return null;

        return new QuestionStatus(
            question.SolvedAt.HasValue,
            question.AcceptedPostPublicId,
            question.SolvedAt);
    }

    public async Task<(bool Success, string? Error)> MarkQuestionSolvedAsync(
        string discussionPublicId, string postPublicId, string userPublicId)
    {
        // Verify the user is the discussion author (OP)
        var discussion = await context.Discussions
            .Where(d => d.PublicId == discussionPublicId && !d.IsDeleted)
            .Select(d => new { d.Id, CreatedByPublicId = d.CreatedByUser.PublicId })
            .FirstOrDefaultAsync();

        if (discussion is null)
            return (false, "Discussion not found");

        if (discussion.CreatedByPublicId != userPublicId)
            return (false, "Only the question author can mark an answer as accepted");

        var question = await context.DiscussionQuestions
            .AsTracking()
            .Where(q => q.Discussion.PublicId == discussionPublicId)
            .FirstOrDefaultAsync();

        if (question is null)
            return (false, "Not a question discussion");

        var postId = await context.Posts
            .Where(p => p.PublicId == postPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (postId == 0)
            return (false, "Post not found");

        question.AcceptedPostId = postId;
        question.SolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return (true, null);
    }

    // === Gallery ===

    public async Task<string?> GetGalleryLayoutAsync(string discussionPublicId)
    {
        return await context.DiscussionGalleries
            .Where(g => g.Discussion.PublicId == discussionPublicId && !g.Discussion.IsDeleted)
            .Select(g => g.Layout)
            .FirstOrDefaultAsync();
    }

    public async Task<List<GalleryImageInfo>> GetGalleryImagesAsync(string discussionPublicId)
    {
        var urlBase = _mediaUrlBase.TrimEnd('/') + "/";

        var images = await context.GalleryImages
            .Where(gi => gi.Gallery.Discussion.PublicId == discussionPublicId
                && !gi.Gallery.Discussion.IsDeleted
                && !gi.Media.IsDeleted)
            .OrderBy(gi => gi.DisplayOrder)
            .Select(gi => new
            {
                gi.Media.StoragePath,
                gi.Media.ThumbnailPath,
                gi.Media.BlurDataUri
            })
            .ToListAsync();

        return images
            .Select(m => new GalleryImageInfo(
                urlBase + m.StoragePath.Replace('\\', '/'),
                m.ThumbnailPath != null ? urlBase + m.ThumbnailPath.Replace('\\', '/') : null,
                m.BlurDataUri))
            .ToList();
    }

    // === Debate ===

    public async Task<DebateInfo?> GetDebateInfoAsync(string discussionPublicId)
    {
        var debate = await context.DiscussionDebates
            .Where(d => d.Discussion.PublicId == discussionPublicId && !d.Discussion.IsDeleted)
            .Select(d => new
            {
                d.AllowNeutral,
                Positions = d.Positions
                    .OrderBy(p => p.Index)
                    .Select(p => new { p.Id, p.Label, p.Index })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (debate is null) return null;

        // Count posts per position
        var positionIds = debate.Positions.Select(p => p.Id).ToList();
        var positionCounts = await context.PostDebatePositions
            .Where(pp => positionIds.Contains(pp.PositionId))
            .GroupBy(pp => pp.PositionId)
            .Select(g => new { PositionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PositionId, x => x.Count);

        // Get post → position mapping
        var postPositions = await context.PostDebatePositions
            .Where(pp => positionIds.Contains(pp.PositionId))
            .Select(pp => new { PostPublicId = pp.Post.PublicId, pp.PositionId })
            .ToDictionaryAsync(x => x.PostPublicId, x => x.PositionId);

        var positions = debate.Positions.Select(p => new DebatePositionData(
            p.Id,
            p.Label,
            p.Index,
            positionCounts.GetValueOrDefault(p.Id, 0)
        )).ToList();

        return new DebateInfo(positions, debate.AllowNeutral, postPositions);
    }

    public async Task<(bool Success, string? Error)> SetPostDebatePositionAsync(
        string discussionPublicId, string postPublicId, int positionId)
    {
        // Verify position belongs to this debate
        var debate = await context.DiscussionDebates
            .Include(d => d.Positions)
            .Where(d => d.Discussion.PublicId == discussionPublicId && !d.Discussion.IsDeleted)
            .FirstOrDefaultAsync();

        if (debate is null)
            return (false, "Debate not found");

        if (!debate.Positions.Any(p => p.Id == positionId))
            return (false, "Invalid position");

        var postId = await context.Posts
            .Where(p => p.PublicId == postPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (postId == 0)
            return (false, "Post not found");

        // Upsert position
        var existing = await context.PostDebatePositions
            .AsTracking()
            .FirstOrDefaultAsync(pp => pp.PostId == postId);

        if (existing is not null)
        {
            existing.PositionId = positionId;
        }
        else
        {
            context.PostDebatePositions.Add(new Database.Entities.PostDebatePositionDatabaseEntity
            {
                PostId = postId,
                PositionId = positionId
            });
        }

        await context.SaveChangesAsync();
        return (true, null);
    }

    // === Link ===

    public async Task<LinkInfo?> GetLinkInfoAsync(string discussionPublicId)
    {
        var link = await context.DiscussionLinks
            .Where(l => l.Discussion.PublicId == discussionPublicId && !l.Discussion.IsDeleted)
            .Select(l => new { l.Url, l.Title, l.Description, l.ImageUrl, l.Domain })
            .FirstOrDefaultAsync();

        if (link is null) return null;

        return new LinkInfo(link.Url, link.Title, link.Description, link.ImageUrl, link.Domain);
    }

    // === Journal ===

    public async Task<JournalInfo?> GetJournalInfoAsync(string discussionPublicId)
    {
        var journal = await context.DiscussionJournals
            .Where(j => j.Discussion.PublicId == discussionPublicId && !j.Discussion.IsDeleted)
            .Select(j => new { j.Id })
            .FirstOrDefaultAsync();

        if (journal is null) return null;

        var entryPostIds = await context.JournalEntryPosts
            .Where(e => e.JournalId == journal.Id && !e.Post.IsDeleted)
            .OrderBy(e => e.Post.CreatedAt)
            .Select(e => e.Post.PublicId)
            .ToListAsync();

        return new JournalInfo(entryPostIds);
    }

    public async Task<(bool Success, string? Error)> AddJournalEntryAsync(
        string discussionPublicId, string postPublicId, string userPublicId)
    {
        // Verify the user is the discussion author (OP)
        var discussion = await context.Discussions
            .Where(d => d.PublicId == discussionPublicId && !d.IsDeleted)
            .Select(d => new { CreatedByPublicId = d.CreatedByUser.PublicId })
            .FirstOrDefaultAsync();

        if (discussion is null)
            return (false, "Discussion not found");

        if (discussion.CreatedByPublicId != userPublicId)
            return (false, "Only the journal author can add updates");

        var journal = await context.DiscussionJournals
            .Where(j => j.Discussion.PublicId == discussionPublicId)
            .FirstOrDefaultAsync();

        if (journal is null)
            return (false, "Not a journal discussion");

        var postId = await context.Posts
            .Where(p => p.PublicId == postPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (postId == 0)
            return (false, "Post not found");

        // Check if already a journal entry
        var exists = await context.JournalEntryPosts
            .AnyAsync(e => e.PostId == postId);

        if (exists)
            return (false, "Post is already a journal entry");

        context.JournalEntryPosts.Add(new Database.Entities.JournalEntryPostDatabaseEntity
        {
            PostId = postId,
            JournalId = journal.Id
        });

        await context.SaveChangesAsync();
        return (true, null);
    }
}
