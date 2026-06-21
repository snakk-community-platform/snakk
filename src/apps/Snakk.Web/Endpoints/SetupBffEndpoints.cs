namespace Snakk.Web.Endpoints;

public static class SetupBffEndpoints
{
    private static int _triggered;

    public static void MapSetupBffEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/bff/setup/warm-cache", WarmCacheAsync)
            .ExcludeFromDescription();
    }

    private static IResult WarmCacheAsync(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        if (Interlocked.CompareExchange(ref _triggered, 1, 0) != 0)
            return Results.Ok();

        _ = RetryWarmAsync(httpClientFactory, logger);
        return Results.Accepted();
    }

    private static async Task RetryWarmAsync(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        var client = httpClientFactory.CreateClient("InternalApi");
        var delaySeconds = 0;

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            if (delaySeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

            try
            {
                var response = await client.PostAsync("/api/internal/cache/warm", content: null);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[CacheWarmup] attempt {Attempt} failed", attempt);
            }

            delaySeconds = delaySeconds == 0 ? 2 : Math.Min(delaySeconds * 2, 30);
        }

        logger.LogError("[CacheWarmup] gave up after {MaxAttempts} attempts", 6);
    }
}
