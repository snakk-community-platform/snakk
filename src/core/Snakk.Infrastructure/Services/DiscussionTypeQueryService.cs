using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class DiscussionTypeQueryService(SnakkDbContext context, IFileStorage fileStorage) : IDiscussionTypeQueryService
{
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

    // === Images ===

    public async Task<(string? Layout, bool IsSpoiler)> GetImagesInfoAsync(string discussionPublicId)
    {
        var info = await context.DiscussionImages
            .Where(g => g.Discussion.PublicId == discussionPublicId && !g.Discussion.IsDeleted)
            .Select(g => new { g.Layout, g.IsSpoiler })
            .FirstOrDefaultAsync();

        return (info?.Layout, info?.IsSpoiler ?? false);
    }

    public async Task<List<ImagesImageInfo>> GetImagesListAsync(string discussionPublicId)
    {
        var images = await context.DiscussionImageAttachments
            .Where(gi => gi.DiscussionTypeImage.Discussion.PublicId == discussionPublicId
                && !gi.DiscussionTypeImage.Discussion.IsDeleted
                && !gi.Image.IsDeleted)
            .OrderBy(gi => gi.DisplayOrder)
            .Select(gi => new
            {
                gi.Image.StoragePath,
                gi.Image.ThumbnailPath,
                gi.Image.ThumbnailWidth,
                gi.Image.ThumbnailHeight,
                gi.Image.MediumThumbnailPath,
                gi.Image.MediumThumbnailWidth,
                gi.Image.MediumThumbnailHeight,
                gi.Image.BlurDataUri,
                gi.Image.Width,
                gi.Image.Height
            })
            .ToListAsync();

        return images
            .Select(m => new ImagesImageInfo(
                fileStorage.GetPublicUrl(m.StoragePath),
                m.ThumbnailPath != null ? fileStorage.GetPublicUrl(m.ThumbnailPath) : null,
                m.MediumThumbnailPath != null ? fileStorage.GetPublicUrl(m.MediumThumbnailPath) : null,
                m.BlurDataUri,
                m.Width,
                m.Height,
                m.ThumbnailWidth,
                m.ThumbnailHeight,
                m.MediumThumbnailWidth,
                m.MediumThumbnailHeight))
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

        // Single query — used for both user-per-position counts and post→position mapping.
        var positionIds = debate.Positions.Select(p => p.Id).ToList();
        var allPostPositions = await context.DiscussionDebatePostPositions
            .Where(pp => positionIds.Contains(pp.PositionId))
            .Select(pp => new {
                pp.PositionId,
                pp.Post.CreatedByUserId,
                pp.Post.CreatedAt,
                PostPublicId = pp.Post.PublicId
            })
            .ToListAsync();

        var positionCounts = allPostPositions
            .GroupBy(x => x.CreatedByUserId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .GroupBy(x => x.PositionId)
            .ToDictionary(g => g.Key, g => g.Count());

        var postPositions = allPostPositions
            .ToDictionary(x => x.PostPublicId, x => x.PositionId);

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
        var existing = await context.DiscussionDebatePostPositions
            .AsTracking()
            .FirstOrDefaultAsync(pp => pp.PostId == postId);

        if (existing is not null)
        {
            existing.PositionId = positionId;
        }
        else
        {
            context.DiscussionDebatePostPositions.Add(new Database.Entities.DiscussionTypeDebatePostPositionDatabaseEntity
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
            .Select(l => new { l.Url, l.Title, l.Description, l.ImageUrl, l.Domain, l.OEmbedHtml, l.ImagePath, l.ImageBlurDataUri, l.IsInternal, l.ImageWidth, l.ImageHeight })
            .FirstOrDefaultAsync();

        if (link is null) return null;

        return new LinkInfo(link.Url, link.Title, link.Description, link.ImageUrl, link.Domain, link.OEmbedHtml, link.ImagePath, link.ImageBlurDataUri, link.IsInternal, link.ImageWidth, link.ImageHeight);
    }

    // === Journal ===

    public async Task<JournalInfo?> GetJournalInfoAsync(string discussionPublicId)
    {
        var journal = await context.DiscussionJournals
            .Where(j => j.Discussion.PublicId == discussionPublicId && !j.Discussion.IsDeleted)
            .Select(j => new { j.Id })
            .FirstOrDefaultAsync();

        if (journal is null) return null;

        var entryPostIds = await context.DiscussionJournalEntryPosts
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
        var exists = await context.DiscussionJournalEntryPosts
            .AnyAsync(e => e.PostId == postId);

        if (exists)
            return (false, "Post is already a journal entry");

        context.DiscussionJournalEntryPosts.Add(new Database.Entities.DiscussionTypeJournalEntryPostDatabaseEntity
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

        var officialAnswers = await context.DiscussionIamaOfficialAnswers
            .Where(oa => oa.IamaId == iama.Id)
            .Select(oa => new
            {
                QuestionPublicId = oa.QuestionPost.PublicId,
                AnswerPublicId = oa.AnswerPost.PublicId
            })
            .ToDictionaryAsync(x => x.QuestionPublicId, x => x.AnswerPublicId);

        var bestQuestions = await context.DiscussionIamaBestQuestions
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
        var existing = await context.DiscussionIamaOfficialAnswers
            .AsTracking()
            .FirstOrDefaultAsync(oa => oa.IamaId == iama.Id && oa.QuestionPostId == questionPostId);

        if (existing is not null)
        {
            existing.AnswerPostId = answerPostId;
        }
        else
        {
            context.DiscussionIamaOfficialAnswers.Add(new Database.Entities.DiscussionTypeIamaOfficialAnswerDatabaseEntity
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
        var existingBest = await context.DiscussionIamaBestQuestions
            .Where(bq => bq.IamaId == iama.Id)
            .ToListAsync();

        context.DiscussionIamaBestQuestions.RemoveRange(existingBest);

        // Bulk-fetch all post IDs in one query instead of N sequential lookups
        var idMap = await context.Posts
            .Where(p => postPublicIds.Contains(p.PublicId) && !p.IsDeleted)
            .Select(p => new { p.PublicId, p.Id })
            .ToDictionaryAsync(p => p.PublicId, p => p.Id);

        for (var i = 0; i < postPublicIds.Count; i++)
        {
            if (!idMap.TryGetValue(postPublicIds[i], out var postId))
                return (false, $"Post '{postPublicIds[i]}' not found");

            context.DiscussionIamaBestQuestions.Add(new Database.Entities.DiscussionTypeIamaBestQuestionDatabaseEntity
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
