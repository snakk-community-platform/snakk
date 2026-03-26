using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class DiscussionExtensionService(SnakkDbContext context) : IDiscussionExtensionService
{
    public async Task CreateQuestionAsync(string discussionPublicId)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionQuestions.Add(new DiscussionQuestionDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync();
    }

    public async Task CreateGuideAsync(string discussionPublicId)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionGuides.Add(new DiscussionGuideDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync();
    }

    public async Task CreatePollAsync(
        string discussionPublicId,
        List<string> options,
        bool allowMultipleChoices = false,
        bool allowChangeVote = false,
        DateTime? closesAt = null)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        var poll = new DiscussionPollDatabaseEntity
        {
            DiscussionId = discussionId,
            AllowMultipleChoices = allowMultipleChoices,
            AllowChangeVote = allowChangeVote,
            ClosesAt = closesAt
        };

        context.DiscussionPolls.Add(poll);
        await context.SaveChangesAsync();

        for (var i = 0; i < options.Count; i++)
        {
            context.PollOptions.Add(new PollOptionDatabaseEntity
            {
                PollId = poll.Id,
                Text = options[i],
                DisplayOrder = i
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task CreateLinkAsync(
        string discussionPublicId,
        string url,
        string? title = null,
        string? description = null,
        string? imageUrl = null,
        string? domain = null)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionLinks.Add(new DiscussionLinkDatabaseEntity
        {
            DiscussionId = discussionId,
            Url = url,
            Title = title,
            Description = description,
            ImageUrl = imageUrl,
            Domain = domain
        });

        await context.SaveChangesAsync();
    }

    public async Task CreateDebateAsync(
        string discussionPublicId,
        List<string> positionLabels,
        bool allowNeutral = false)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        var debate = new DiscussionDebateDatabaseEntity
        {
            DiscussionId = discussionId,
            AllowNeutral = allowNeutral
        };

        context.DiscussionDebates.Add(debate);
        await context.SaveChangesAsync();

        for (var i = 0; i < positionLabels.Count; i++)
        {
            context.DiscussionDebatePositions.Add(new DiscussionDebatePositionDatabaseEntity
            {
                DebateId = debate.Id,
                Index = i,
                Label = positionLabels[i]
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task MarkQuestionSolvedAsync(string discussionPublicId, string acceptedPostPublicId)
    {
        var question = await context.DiscussionQuestions
            .AsTracking()
            .Where(q => q.Discussion.PublicId == discussionPublicId && !q.Discussion.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Question not found");

        var postId = await context.Posts
            .Where(p => p.PublicId == acceptedPostPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (postId == 0)
            throw new InvalidOperationException("Post not found");

        question.AcceptedPostId = postId;
        question.SolvedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task CreateJournalAsync(string discussionPublicId)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionJournals.Add(new DiscussionJournalDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync();
    }

    public async Task AddJournalEntryAsync(string discussionPublicId, string postPublicId)
    {
        var journal = await context.DiscussionJournals
            .Where(j => j.Discussion.PublicId == discussionPublicId && !j.Discussion.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Journal not found");

        var postId = await context.Posts
            .Where(p => p.PublicId == postPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (postId == 0)
            throw new InvalidOperationException("Post not found");

        context.JournalEntryPosts.Add(new JournalEntryPostDatabaseEntity
        {
            PostId = postId,
            JournalId = journal.Id
        });

        await context.SaveChangesAsync();
    }

    private async Task<int> GetDiscussionIdAsync(string publicId)
    {
        var id = await context.Discussions
            .Where(d => d.PublicId == publicId && !d.IsDeleted)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        return id == 0
            ? throw new InvalidOperationException($"Discussion '{publicId}' not found")
            : id;
    }
}
