namespace Snakk.Application.Repositories;

public interface IDashboardChartRepository
{
    Task<List<DailyActivityData>> GetDailyActivityAsync(
        string scopeType, string scopePublicId, int days, CancellationToken ct = default);

    Task<List<WeeklyModerationData>> GetWeeklyModerationAsync(
        string scopeType, string scopePublicId, int weeks, CancellationToken ct = default);

    Task<List<ReactionBreakdownData>> GetReactionBreakdownAsync(
        string scopeType, string scopePublicId, int days, CancellationToken ct = default);

    Task<List<ContentTypeBreakdownData>> GetContentTypeBreakdownAsync(
        string scopeType, string scopePublicId, int days, CancellationToken ct = default);

    Task<List<DailyEngagementData>> GetDailyEngagementAsync(
        string scopeType, string scopePublicId, int days, CancellationToken ct = default);

    Task<List<TopDiscussionData>> GetTopDiscussionsAsync(
        string scopeType, string scopePublicId, int days, int count = 10, CancellationToken ct = default);

    Task<List<TopContributorData>> GetTopContributorsForScopeAsync(
        string scopeType, string scopePublicId, int days, int limit = 10, CancellationToken ct = default);

    Task<List<DailyRegistrationData>> GetDailyRegistrationsAsync(
        int days, CancellationToken ct = default);

    Task<(int Dau, int Wau, int Mau, int Total)> GetUserActivityCountsAsync(
        CancellationToken ct = default);
}

public record DailyActivityData(DateTime Date, int Discussions, int Posts);

public record WeeklyModerationData(DateTime WeekStart, int ReportsOpened, int ReportsResolved);

public record ReactionBreakdownData(string ReactionType, int Count);

public record ContentTypeBreakdownData(string DiscussionType, int Count);

public record DailyEngagementData(DateTime Date, int Reactions);

public record TopDiscussionData(string Title, string Slug, string SpaceSlug, int PostCount, int ReactionCount);

public record TopContributorData(string UserPublicId, string DisplayName, string? AvatarThumbnailFileName, int PostCount, int DiscussionCount);

public record DailyRegistrationData(DateTime Date, int Count);
