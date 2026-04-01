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
        string discussionPublicId, string postPublicId, int positionId, string userPublicId)
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

        var post = await context.Posts
            .Where(p => p.PublicId == postPublicId && !p.IsDeleted)
            .Select(p => new { p.Id, CreatedByPublicId = p.CreatedByUser.PublicId })
            .FirstOrDefaultAsync();

        if (post is null)
            return (false, "Post not found");

        if (post.CreatedByPublicId != userPublicId)
            return (false, "You can only set the debate position on your own posts");

        var postId = post.Id;

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
            .Select(l => new { l.Url, l.Title, l.Description, l.ImageUrl, l.Domain, l.OEmbedHtml, l.LocalImagePath, l.ImageBlurDataUri, l.IsInternal })
            .FirstOrDefaultAsync();

        if (link is null) return null;

        return new LinkInfo(link.Url, link.Title, link.Description, link.ImageUrl, link.Domain, link.OEmbedHtml, link.LocalImagePath, link.ImageBlurDataUri, link.IsInternal);
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

    // === IAMA ===

    public async Task<IamaInfo?> GetIamaInfoAsync(string discussionPublicId)
    {
        var iama = await context.DiscussionIamas
            .Where(i => i.Discussion.PublicId == discussionPublicId && !i.Discussion.IsDeleted)
            .Select(i => new
            {
                i.Phase,
                i.IsScheduled,
                i.ScheduledStartUtc,
                i.ScheduledEndUtc,
                i.VerificationNote,
                i.Id
            })
            .FirstOrDefaultAsync();

        if (iama is null) return null;

        var officialAnswers = await context.IamaOfficialAnswers
            .Where(oa => oa.IamaId == iama.Id)
            .Select(oa => new
            {
                QuestionPublicId = oa.QuestionPost.PublicId,
                AnswerPublicId = oa.AnswerPost.PublicId
            })
            .ToDictionaryAsync(x => x.QuestionPublicId, x => x.AnswerPublicId);

        var bestQuestions = await context.IamaBestQuestions
            .Where(bq => bq.IamaId == iama.Id)
            .OrderBy(bq => bq.DisplayOrder)
            .Select(bq => bq.Post.PublicId)
            .ToListAsync();

        return new IamaInfo(
            iama.Phase,
            iama.IsScheduled,
            iama.ScheduledStartUtc,
            iama.ScheduledEndUtc,
            iama.VerificationNote,
            officialAnswers,
            bestQuestions);
    }

    public async Task<(bool Success, string? Error)> MarkIamaOfficialAnswerAsync(
        string discussionPublicId, string questionPostPublicId, string answerPostPublicId, string userPublicId)
    {
        // Verify the user is the host (discussion creator)
        var discussion = await context.Discussions
            .Where(d => d.PublicId == discussionPublicId && !d.IsDeleted)
            .Select(d => new { d.Id, CreatedByPublicId = d.CreatedByUser.PublicId })
            .FirstOrDefaultAsync();

        if (discussion is null)
            return (false, "Discussion not found");

        if (discussion.CreatedByPublicId != userPublicId)
            return (false, "Only the AMA host can mark official answers");

        var iama = await context.DiscussionIamas
            .Where(i => i.DiscussionId == discussion.Id)
            .Select(i => new { i.Id, i.Phase })
            .FirstOrDefaultAsync();

        if (iama is null)
            return (false, "Not an AMA discussion");

        if (iama.Phase >= 3) // Archived
            return (false, "Cannot modify an archived AMA");

        var questionPostId = await context.Posts
            .Where(p => p.PublicId == questionPostPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (questionPostId == 0)
            return (false, "Question post not found");

        var answerPostId = await context.Posts
            .Where(p => p.PublicId == answerPostPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (answerPostId == 0)
            return (false, "Answer post not found");

        // Upsert: remove existing answer for this question if any
        var existing = await context.IamaOfficialAnswers
            .AsTracking()
            .FirstOrDefaultAsync(oa => oa.IamaId == iama.Id && oa.QuestionPostId == questionPostId);

        if (existing is not null)
        {
            existing.AnswerPostId = answerPostId;
        }
        else
        {
            context.IamaOfficialAnswers.Add(new Database.Entities.IamaOfficialAnswerDatabaseEntity
            {
                IamaId = iama.Id,
                QuestionPostId = questionPostId,
                AnswerPostId = answerPostId
            });
        }

        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetIamaBestQuestionsAsync(
        string discussionPublicId, List<string> postPublicIds, string userPublicId)
    {
        if (postPublicIds.Count > 5)
            return (false, "Cannot mark more than 5 best questions");

        var discussion = await context.Discussions
            .Where(d => d.PublicId == discussionPublicId && !d.IsDeleted)
            .Select(d => new { d.Id, CreatedByPublicId = d.CreatedByUser.PublicId })
            .FirstOrDefaultAsync();

        if (discussion is null)
            return (false, "Discussion not found");

        if (discussion.CreatedByPublicId != userPublicId)
            return (false, "Only the AMA host can set best questions");

        var iama = await context.DiscussionIamas
            .Where(i => i.DiscussionId == discussion.Id)
            .Select(i => new { i.Id })
            .FirstOrDefaultAsync();

        if (iama is null)
            return (false, "Not an AMA discussion");

        // Remove existing best questions
        var existingBest = await context.IamaBestQuestions
            .Where(bq => bq.IamaId == iama.Id)
            .ToListAsync();

        context.IamaBestQuestions.RemoveRange(existingBest);

        // Add new best questions
        for (var i = 0; i < postPublicIds.Count; i++)
        {
            var postId = await context.Posts
                .Where(p => p.PublicId == postPublicIds[i] && !p.IsDeleted)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (postId == 0)
                return (false, $"Post '{postPublicIds[i]}' not found");

            context.IamaBestQuestions.Add(new Database.Entities.IamaBestQuestionDatabaseEntity
            {
                IamaId = iama.Id,
                PostId = postId,
                DisplayOrder = i
            });
        }

        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> TransitionIamaPhaseAsync(
        string discussionPublicId, int newPhase, string userPublicId)
    {
        if (newPhase is < 0 or > 3)
            return (false, "Invalid phase");

        var discussion = await context.Discussions
            .AsTracking()
            .Where(d => d.PublicId == discussionPublicId && !d.IsDeleted)
            .Include(d => d.CreatedByUser)
            .FirstOrDefaultAsync();

        if (discussion is null)
            return (false, "Discussion not found");

        if (discussion.CreatedByUser.PublicId != userPublicId)
            return (false, "Only the AMA host can change the phase");

        var iama = await context.DiscussionIamas
            .AsTracking()
            .Where(i => i.DiscussionId == discussion.Id)
            .FirstOrDefaultAsync();

        if (iama is null)
            return (false, "Not an AMA discussion");

        if (newPhase <= iama.Phase)
            return (false, "Cannot move to a previous phase");

        iama.Phase = newPhase;

        // Lock discussion when closing or archiving
        if (newPhase >= 2)
            discussion.IsLocked = true;

        await context.SaveChangesAsync();
        return (true, null);
    }
}
