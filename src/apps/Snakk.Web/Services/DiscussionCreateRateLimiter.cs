using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace Snakk.Web.Services;

public sealed class DiscussionCreateRateLimiter(IConfiguration configuration) : IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext> _limiter =
        configuration.GetValue<bool>("DisableRateLimiting")
            ? PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetNoLimiter("disabled"))
            : PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
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
