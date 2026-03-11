namespace Snakk.Application.Repositories;

public interface IDashboardChartRepository
{
    Task<List<DailyActivityData>> GetDailyActivityAsync(
        string scopeType, string scopePublicId, int days);

    Task<List<WeeklyModerationData>> GetWeeklyModerationAsync(
        string scopeType, string scopePublicId, int weeks);

    Task<List<ReactionBreakdownData>> GetReactionBreakdownAsync(
        string scopeType, string scopePublicId, int days);

    Task<List<ContentTypeBreakdownData>> GetContentTypeBreakdownAsync(
        string scopeType, string scopePublicId, int days);

    Task<List<DailyEngagementData>> GetDailyEngagementAsync(
        string scopeType, string scopePublicId, int days);

    Task<List<TopDiscussionData>> GetTopDiscussionsAsync(
        string scopeType, string scopePublicId, int days, int count = 10);
}

public record DailyActivityData(DateTime Date, int Discussions, int Posts);

public record WeeklyModerationData(DateTime WeekStart, int ReportsOpened, int ReportsResolved);

public record ReactionBreakdownData(string ReactionType, int Count);

public record ContentTypeBreakdownData(string DiscussionType, int Count);

public record DailyEngagementData(DateTime Date, int Reactions);

public record TopDiscussionData(string Title, string Slug, string SpaceSlug, int PostCount, int ReactionCount);
