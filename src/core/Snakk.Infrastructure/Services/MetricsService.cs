namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;

public class MetricsService(SnakkDbContext context)
{
    private readonly SnakkDbContext _context = context;

    /// <summary>
    /// Increment a metric across all relevant scopes (Global, Space, Hub, Community) in a single query
    /// </summary>
    public async Task IncrementMetricAsync(
        UserId userId,
        string metricType,
        int? spaceId = null,
        int? hubId = null,
        int? communityId = null,
        int amount = 1)
    {
        // Build UPSERT query for all scopes
        // This single query updates 1-4 rows (Global + Space + Hub + Community)
        var sql = @"
            INSERT INTO ""UserMetric"" (""UserId"", ""MetricType"", ""Scope"", ""ScopeId"", ""Value"", ""LastUpdated"")
            VALUES
                ({0}, {1}, 'Global', NULL, {2}, NOW())";

        var parameters = new List<object> { await GetUserIdAsync(userId), metricType, amount };
        var paramIndex = 3;

        if (spaceId.HasValue)
        {
            sql += $@",
                ({0}, {1}, 'Space', {{{paramIndex}}}, {2}, NOW())";
            parameters.Add(spaceId.Value);
            paramIndex++;
        }

        if (hubId.HasValue)
        {
            sql += $@",
                ({0}, {1}, 'Hub', {{{paramIndex}}}, {2}, NOW())";
            parameters.Add(hubId.Value);
            paramIndex++;
        }

        if (communityId.HasValue)
        {
            sql += $@",
                ({0}, {1}, 'Community', {{{paramIndex}}}, {2}, NOW())";
            parameters.Add(communityId.Value);
        }

        sql += @"
            ON CONFLICT (""UserId"", ""MetricType"", ""Scope"", ""ScopeId"")
            DO UPDATE SET
                ""Value"" = ""UserMetric"".""Value"" + {2},
                ""LastUpdated"" = NOW()";

        await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray());
    }

    /// <summary>
    /// Get the current value of a specific metric
    /// </summary>
    public async Task<int> GetMetricValueAsync(
        UserId userId,
        string metricType,
        string scope = "Global",
        int? scopeId = null)
    {
        var userIdInt = await GetUserIdAsync(userId);

        return await _context.Database
            .SqlQuery<int>($@"
                SELECT COALESCE(""Value"", 0)
                FROM ""UserMetric""
                WHERE ""UserId"" = {userIdInt}
                  AND ""MetricType"" = {metricType}
                  AND ""Scope"" = {scope}
                  AND (""ScopeId"" = {scopeId} OR (""ScopeId"" IS NULL AND {scopeId} IS NULL))")
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all metrics for a user
    /// </summary>
    public async Task<Dictionary<string, int>> GetUserMetricsAsync(UserId userId, string scope = "Global", int? scopeId = null)
    {
        var userIdInt = await GetUserIdAsync(userId);

        var metrics = await _context.Database
            .SqlQuery<MetricResult>($@"
                SELECT ""MetricType"", ""Value""
                FROM ""UserMetric""
                WHERE ""UserId"" = {userIdInt}
                  AND ""Scope"" = {scope}
                  AND (""ScopeId"" = {scopeId} OR (""ScopeId"" IS NULL AND {scopeId} IS NULL))")
            .ToListAsync();

        return metrics.ToDictionary(m => m.MetricType, m => m.Value);
    }

    /// <summary>
    /// Count how many scopes have a metric value above a threshold
    /// Example: How many spaces has the user posted in at least 10 times?
    /// </summary>
    public async Task<int> CountScopesAboveThresholdAsync(
        UserId userId,
        string metricType,
        string scope,
        int threshold)
    {
        var userIdInt = await GetUserIdAsync(userId);

        return await _context.Database
            .SqlQuery<int>($@"
                SELECT COUNT(*)
                FROM ""UserMetric""
                WHERE ""UserId"" = {userIdInt}
                  AND ""MetricType"" = {metricType}
                  AND ""Scope"" = {scope}
                  AND ""Value"" >= {threshold}")
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Helper to resolve PublicId to internal database ID
    /// </summary>
    private async Task<int> GetUserIdAsync(UserId publicUserId)
    {
        var user = await _context.Users
            .Where(u => u.PublicId == publicUserId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (user == 0)
            throw new InvalidOperationException($"User with PublicId '{publicUserId}' not found");

        return user;
    }

    // Helper class for SQL query results
    private class MetricResult
    {
        public string MetricType { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
