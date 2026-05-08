using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Snakk.Web.Services;

public sealed class DiscussionCreateRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext> _limiter =
        PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var key = string.IsNullOrEmpty(userId)
                ? $"ip:{ctx.Connection.RemoteIpAddress}"
                : $"user:{userId}";
            return RateLimitPartition.GetTokenBucketLimiter(key,
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 3,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(6),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    public ValueTask<RateLimitLease> AcquireAsync(HttpContext context)
        => _limiter.AcquireAsync(context, permitCount: 1);

    public void Dispose() => _limiter.Dispose();
}
