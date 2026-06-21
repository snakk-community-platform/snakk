namespace Snakk.Infrastructure.EventHandlers.Trending;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

internal static class TrendScoreCalculator
{
    private const double PostWeight = 3.0;
    private const double ReactionWeight = 1.0;
    private const double Gravity = 1.8;
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(48);

    public static async Task RecalculateAsync(SnakkDbContext context, string discussionPublicId, TimeSpan? window = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - (window ?? DefaultWindow);

        var discussion = await context.Discussions
            .AsTracking()
            .Where(d => d.PublicId == discussionPublicId && !d.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (discussion is null)
            return;

        var postTimes = await context.Posts
            .Where(p => p.DiscussionId == discussion.Id && p.CreatedAt >= cutoff && !p.IsDeleted)
            .Select(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var reactionTimes = await context.PostReactions
            .Where(r => r.Post.DiscussionId == discussion.Id && r.CreatedAt >= cutoff)
            .Select(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        discussion.TrendScore =
            postTimes.Sum(t => PostWeight / Math.Pow((now - t).TotalHours + 2, Gravity))
            + reactionTimes.Sum(t => ReactionWeight / Math.Pow((now - t).TotalHours + 2, Gravity));

        await context.SaveChangesAsync(cancellationToken);
    }
}
