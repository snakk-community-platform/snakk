using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

// User-independent poll snapshot — safe to share across all viewers.
// User vote state is fetched separately and overlaid on top.
// Raw (unmasked) vote counts are stored so IsClosed and secret masking
// can be derived at serve time from the cached ClosesAt timestamp.
internal sealed record CachedPollData(
    bool AllowMultipleChoices,
    bool AllowChangeVote,
    DateTime? ClosesAt,
    bool IsSecret,
    bool IsSegmented,
    string? SegmentLabel,
    string? SegmentOptionA,
    string? SegmentOptionB,
    int TotalVotes,
    List<PollOptionData> Options,
    List<int> OptionIds,
    List<PollOptionSegmentData>? SegmentVotes);

public class PollService(SnakkDbContext context, HybridCache cache) : IPollService
{
    // Write-path invalidation (VoteAsync / RemoveVoteAsync) covers freshness.
    // IsClosed and vote masking are computed at serve time from the cached ClosesAt,
    // so the short 15s TTL is no longer needed to catch poll close transitions.
    private static readonly HybridCacheEntryOptions PollCacheOptions = new() { Expiration = TimeSpan.FromHours(24) };

    public async Task<PollData?> GetPollAsync(string discussionPublicId, string? userPublicId = null, CancellationToken ct = default)
    {
        // Fetch user-independent poll data from cache (or DB on miss)
        var cached = await cache.GetOrCreateAsync(
            $"poll:{discussionPublicId}",
            async innerCt => await FetchCachedPollDataAsync(discussionPublicId, innerCt),
            PollCacheOptions,
            cancellationToken: ct);

        if (cached is null) return null;

        // Compute close state at serve time from the cached timestamp (not baked into the cache).
        var isClosed = cached.ClosesAt.HasValue && cached.ClosesAt.Value <= DateTime.UtcNow;
        var maskVotes = cached.IsSecret && !isClosed;

        var options = maskVotes
            ? cached.Options.Select(o => new PollOptionData(o.Id, o.Text, 0, o.DisplayOrder)).ToList()
            : cached.Options;

        var totalVotes = maskVotes ? 0 : cached.TotalVotes;

        var segmentVotes = maskVotes && cached.SegmentVotes is not null
            ? cached.SegmentVotes.Select(s => new PollOptionSegmentData(s.OptionId, 0, 0)).ToList()
            : cached.SegmentVotes;

        // Overlay the requesting user's own votes — single JOIN, not two sequential queries.
        int? userSegmentIndex = null;
        var userVotedIds = new List<int>();
        if (!string.IsNullOrEmpty(userPublicId))
        {
            var userVotes = await context.DiscussionPollVotes
                .Where(v => cached.OptionIds.Contains(v.OptionId)
                            && v.User.PublicId == userPublicId
                            && !v.User.IsDeleted)
                .Select(v => new { v.OptionId, v.SegmentIndex })
                .ToListAsync(ct);

            userVotedIds = userVotes.Select(v => v.OptionId).ToList();
            if (cached.IsSegmented)
                userSegmentIndex = userVotes.FirstOrDefault()?.SegmentIndex;
        }

        return new PollData(
            options,
            cached.AllowMultipleChoices,
            cached.AllowChangeVote,
            cached.ClosesAt,
            isClosed,
            cached.IsSecret,
            totalVotes,
            userVotedIds,
            IsSegmented: cached.IsSegmented,
            SegmentLabel: cached.SegmentLabel,
            SegmentOptionA: cached.SegmentOptionA,
            SegmentOptionB: cached.SegmentOptionB,
            SegmentVotes: segmentVotes,
            UserSegmentIndex: userSegmentIndex);
    }

    private async Task<CachedPollData?> FetchCachedPollDataAsync(string discussionPublicId, CancellationToken ct)
    {
        var poll = await context.DiscussionPolls
            .Where(p => p.Discussion.PublicId == discussionPublicId && !p.Discussion.IsDeleted)
            .Select(p => new
            {
                p.AllowMultipleChoices,
                p.AllowChangeVote,
                p.ClosesAt,
                p.VotesVisible,
                p.IsSegmented,
                p.SegmentLabel,
                p.SegmentOptionA,
                p.SegmentOptionB,
                Options = p.Options
                    .OrderBy(o => o.DisplayOrder)
                    .Select(o => new { o.Id, o.Text, o.VoteCount, o.DisplayOrder })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (poll is null) return null;

        var totalVotes = poll.Options.Sum(o => o.VoteCount);

        // Store raw segment vote counts — masking applied at serve time
        List<PollOptionSegmentData>? segmentVotes = null;
        if (poll.IsSegmented)
        {
            var allOptionIds = poll.Options.Select(o => o.Id).ToList();
            var raw = await context.DiscussionPollVotes
                .Where(v => allOptionIds.Contains(v.OptionId))
                .GroupBy(v => new { v.OptionId, v.SegmentIndex })
                .Select(g => new { g.Key.OptionId, g.Key.SegmentIndex, Count = g.Count() })
                .ToListAsync(ct);

            segmentVotes = allOptionIds.Select(oid => new PollOptionSegmentData(
                oid,
                raw.Where(r => r.OptionId == oid && r.SegmentIndex == 0).Sum(r => r.Count),
                raw.Where(r => r.OptionId == oid && r.SegmentIndex == 1).Sum(r => r.Count)
            )).ToList();
        }

        // Store raw vote counts — IsClosed and secret masking applied at serve time
        var options = poll.Options
            .Select(o => new PollOptionData(o.Id, o.Text, o.VoteCount, o.DisplayOrder))
            .ToList();

        return new CachedPollData(
            poll.AllowMultipleChoices,
            poll.AllowChangeVote,
            poll.ClosesAt,
            !poll.VotesVisible,
            poll.IsSegmented,
            poll.SegmentLabel,
            poll.SegmentOptionA,
            poll.SegmentOptionB,
            totalVotes,
            options,
            poll.Options.Select(o => o.Id).ToList(),
            segmentVotes);
    }

    public async Task<(bool Success, string? Error)> VoteAsync(
        string discussionPublicId, int optionId, string userPublicId, int? segmentIndex = null, CancellationToken ct = default)
    {
        var poll = await context.DiscussionPolls
            .AsTracking()
            .Include(p => p.Options)
            .Where(p => p.Discussion.PublicId == discussionPublicId && !p.Discussion.IsDeleted)
            .FirstOrDefaultAsync(ct);

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
            .FirstOrDefaultAsync(ct);

        if (userId == 0)
            return (false, "User not found");

        // Check existing votes
        var optionIds = poll.Options.Select(o => o.Id).ToList();
        var existingVotes = await context.DiscussionPollVotes
            .Where(v => optionIds.Contains(v.OptionId) && v.UserId == userId)
            .ToListAsync(ct);

        List<int> decrementedOptionIds = [];
        if (existingVotes.Count > 0)
        {
            if (!poll.AllowMultipleChoices && !poll.AllowChangeVote)
                return (false, "You have already voted");

            if (!poll.AllowMultipleChoices && poll.AllowChangeVote)
            {
                // Single choice + change allowed: remove old vote, add new
                foreach (var old in existingVotes)
                {
                    decrementedOptionIds.Add(old.OptionId);
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
            VotedAt = DateTime.UtcNow,
            SegmentIndex = segmentIndex
        });

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return (false, "You have already voted for this option");
        }

        if (decrementedOptionIds.Count > 0)
            await context.DiscussionPollOptions
                .Where(o => decrementedOptionIds.Contains(o.Id))
                .ExecuteUpdateAsync(o => o.SetProperty(x => x.VoteCount, x => x.VoteCount - 1), ct);

        await context.DiscussionPollOptions
            .Where(o => o.Id == optionId)
            .ExecuteUpdateAsync(o => o.SetProperty(x => x.VoteCount, x => x.VoteCount + 1), ct);

        await cache.RemoveAsync($"preview:poll:{discussionPublicId}", ct);
        await cache.RemoveAsync($"poll:{discussionPublicId}", ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveVoteAsync(
        string discussionPublicId, int optionId, string userPublicId, CancellationToken ct = default)
    {
        var poll = await context.DiscussionPolls
            .AsTracking()
            .Include(p => p.Options)
            .Where(p => p.Discussion.PublicId == discussionPublicId && !p.Discussion.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (poll is null)
            return (false, "Poll not found");

        if (!poll.AllowChangeVote)
            return (false, "Changing votes is not allowed for this poll");

        if (poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow)
            return (false, "Poll is closed");

        var userId = await context.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == 0)
            return (false, "User not found");

        var vote = await context.DiscussionPollVotes
            .FirstOrDefaultAsync(v => v.OptionId == optionId && v.UserId == userId, ct);

        if (vote is null)
            return (false, "Vote not found");

        context.DiscussionPollVotes.Remove(vote);
        await context.SaveChangesAsync(ct);

        await context.DiscussionPollOptions
            .Where(o => o.Id == optionId)
            .ExecuteUpdateAsync(o => o.SetProperty(x => x.VoteCount, x => x.VoteCount - 1), ct);

        await cache.RemoveAsync($"preview:poll:{discussionPublicId}", ct);
        await cache.RemoveAsync($"poll:{discussionPublicId}", ct);
        return (true, null);
    }
}
