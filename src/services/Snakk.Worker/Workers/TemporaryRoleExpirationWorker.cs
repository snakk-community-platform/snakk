namespace Snakk.Worker.Workers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Infrastructure.Database;

public class TemporaryRoleExpirationWorker(
    IServiceProvider serviceProvider,
    ILogger<TemporaryRoleExpirationWorker> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<TemporaryRoleExpirationWorker> _logger = logger;
    private DateTime _lastTokenCleanup = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Temporary Role Expiration Worker started");

        // Wait 1 minute before first execution to allow application to fully start
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireTemporaryRolesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Temporary Role Expiration Worker");
            }

            if (DateTime.UtcNow - _lastTokenCleanup > TimeSpan.FromHours(24))
            {
                try
                {
                    await DeleteExpiredRefreshTokensAsync(stoppingToken);
                    _lastTokenCleanup = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning up expired refresh tokens");
                }
            }

            // Run every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("Temporary Role Expiration Worker stopped");
    }

    private async Task ExpireTemporaryRolesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<HybridCache>();

        var now = DateTime.UtcNow;

        // Find all temporary role elevations that have expired but haven't been revoked yet
        var expiredElevations = await context.TemporaryRoleElevations
            .AsTracking()
            .Where(e => e.ExpiresAt <= now && e.RevokedAt == null)
            .Include(e => e.User)
            .ToListAsync(ct);

        if (expiredElevations.Count == 0)
        {
            _logger.LogDebug("No expired temporary role elevations found");
            return;
        }

        _logger.LogInformation("Found {Count} expired temporary role elevations to process", expiredElevations.Count);

        foreach (var elevation in expiredElevations)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                // Mark as revoked (system-revoked, no admin user)
                elevation.RevokedAt = now;
                elevation.RevokedById = null;
                elevation.RevokedReason = "Automatic expiration";

                // Invalidate user's permission cache
                await cache.RemoveAsync($"user_permissions_{elevation.User!.PublicId}");
                await cache.RemoveByTagAsync($"manage_perms_user_{elevation.User!.PublicId}");

                _logger.LogInformation(
                    "Expired temporary role elevation {ElevationId} for user {UserId} ({RoleType} in {Scope}:{ScopeId})",
                    elevation.PublicId,
                    elevation.User.PublicId,
                    elevation.RoleType,
                    elevation.Scope,
                    elevation.ScopeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring temporary role elevation {ElevationId}", elevation.PublicId);
            }
        }

        // Save all changes
        var savedCount = await context.SaveChangesAsync(ct);
        _logger.LogInformation("Expired {Count} temporary role elevations", savedCount);
    }

    private async Task DeleteExpiredRefreshTokensAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();

        var deleted = await context.RefreshTokens
            .Where(t => t.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("Deleted {Count} expired refresh tokens", deleted);
    }
}
