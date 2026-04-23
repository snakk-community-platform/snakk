namespace Snakk.Infrastructure.EventHandlers.Trending;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

internal static class TrendScoreCalculator
{
    private const double PostWeight = 3.0;
    private const double ReactionWeight = 1.0;
    private const double Gravity = 1.8;
    private const int WindowHours = 48;

    public static async Task RecalculateAsync(SnakkDbContext context, string discussionPublicId)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-WindowHours);

        var discussion = await context.Discussions
            .AsTracking()
            .Where(d => d.PublicId == discussionPublicId && !d.IsDeleted)
            .FirstOrDefaultAsync();

        if (discussion is null)
            return;

        var postTimes = await context.Posts
            .Where(p => p.DiscussionId == discussion.Id && p.CreatedAt >= cutoff && !p.IsDeleted)
            .Select(p => p.CreatedAt)
            .ToListAsync();

        var reactionTimes = await context.PostReactions
            .Where(r => r.Post.DiscussionId == discussion.Id && r.CreatedAt >= cutoff)
            .Select(r => r.CreatedAt)
            .ToListAsync();

        discussion.TrendScore =
            postTimes.Sum(t => PostWeight / Math.Pow((now - t).TotalHours + 2, Gravity))
            + reactionTimes.Sum(t => ReactionWeight / Math.Pow((now - t).TotalHours + 2, Gravity));

        await context.SaveChangesAsync();
    }
}
