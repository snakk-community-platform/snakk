using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class PollService(SnakkDbContext context) : IPollService
{
    public async Task<PollData?> GetPollAsync(string discussionPublicId, string? userPublicId = null)
    {
        var poll = await context.DiscussionPolls
            .Where(p => p.Discussion.PublicId == discussionPublicId && !p.Discussion.IsDeleted)
            .Select(p => new
            {
                p.AllowMultipleChoices,
                p.AllowChangeVote,
                p.ClosesAt,
                p.VotesVisible,
                Options = p.Options
                    .OrderBy(o => o.DisplayOrder)
                    .Select(o => new { o.Id, o.Text, o.VoteCount, o.DisplayOrder })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (poll is null) return null;

        var isClosed = poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow;
        var isSecret = !poll.VotesVisible;
        var totalVotes = poll.Options.Sum(o => o.VoteCount);

        // Get user's votes if authenticated
        var userVotedIds = new List<int>();
        if (!string.IsNullOrEmpty(userPublicId))
        {
            var userId = await context.Users
                .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (userId > 0)
            {
                var optionIds = poll.Options.Select(o => o.Id).ToList();
                userVotedIds = await context.DiscussionPollVotes
                    .Where(v => optionIds.Contains(v.OptionId) && v.UserId == userId)
                    .Select(v => v.OptionId)
                    .ToListAsync();
            }
        }

        // When poll is secret and not yet closed, hide vote counts
        var options = isSecret && !isClosed
            ? poll.Options.Select(o => new PollOptionData(o.Id, o.Text, 0, o.DisplayOrder)).ToList()
            : poll.Options.Select(o => new PollOptionData(o.Id, o.Text, o.VoteCount, o.DisplayOrder)).ToList();

        if (isSecret && !isClosed)
            totalVotes = 0;

        return new PollData(
            options,
            poll.AllowMultipleChoices,
            poll.AllowChangeVote,
            poll.ClosesAt,
            isClosed,
            isSecret,
            totalVotes,
            userVotedIds);
    }

    public async Task<(bool Success, string? Error)> VoteAsync(
        string discussionPublicId, int optionId, string userPublicId)
    {
        var poll = await context.DiscussionPolls
            .Include(p => p.Options)
            .Where(p => p.Discussion.PublicId == discussionPublicId && !p.Discussion.IsDeleted)
            .FirstOrDefaultAsync();

        if (poll is null)
            return (false, "Poll not found");

        if (poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow)
            return (false, "Poll is closed");

        var option = poll.Options.FirstOrDefault(o => o.Id == optionId);
        if (option is null)
            return (false, "Option not found");

        var userId = await context.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userId == 0)
            return (false, "User not found");

        // Check existing votes
        var existingVotes = await context.DiscussionPollVotes
            .Where(v => poll.Options.Select(o => o.Id).Contains(v.OptionId) && v.UserId == userId)
            .ToListAsync();

        if (existingVotes.Count > 0)
        {
            if (!poll.AllowMultipleChoices && !poll.AllowChangeVote)
                return (false, "You have already voted");

            if (!poll.AllowMultipleChoices && poll.AllowChangeVote)
            {
                // Single choice + change allowed: remove old vote, add new
                foreach (var old in existingVotes)
                {
                    var oldOption = poll.Options.First(o => o.Id == old.OptionId);
                    oldOption.VoteCount = Math.Max(0, oldOption.VoteCount - 1);
                    context.DiscussionPollVotes.Remove(old);
                }
            }
            else if (poll.AllowMultipleChoices)
            {
                // Multi-choice: check if already voted for this specific option
                if (existingVotes.Any(v => v.OptionId == optionId))
                    return (false, "You have already voted for this option");
            }
        }

        context.DiscussionPollVotes.Add(new DiscussionTypePollVoteDatabaseEntity
        {
            OptionId = optionId,
            UserId = userId,
            VotedAt = DateTime.UtcNow
        });
        option.VoteCount++;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return (false, "You have already voted for this option");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveVoteAsync(
        string discussionPublicId, int optionId, string userPublicId)
    {
        var poll = await context.DiscussionPolls
            .Include(p => p.Options)
            .Where(p => p.Discussion.PublicId == discussionPublicId && !p.Discussion.IsDeleted)
            .FirstOrDefaultAsync();

        if (poll is null)
            return (false, "Poll not found");

        if (!poll.AllowChangeVote)
            return (false, "Changing votes is not allowed for this poll");

        if (poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow)
            return (false, "Poll is closed");

        var userId = await context.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userId == 0)
            return (false, "User not found");

        var vote = await context.DiscussionPollVotes
            .FirstOrDefaultAsync(v => v.OptionId == optionId && v.UserId == userId);

        if (vote is null)
            return (false, "Vote not found");

        var option = poll.Options.FirstOrDefault(o => o.Id == optionId);
        if (option is not null)
            option.VoteCount = Math.Max(0, option.VoteCount - 1);

        context.DiscussionPollVotes.Remove(vote);
        await context.SaveChangesAsync();

        return (true, null);
    }
}
