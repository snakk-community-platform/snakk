namespace Snakk.Api.Endpoints;

using Snakk.Infrastructure.Database.Repositories;

public static class RealtimeInternalEndpoints
{
    public static void MapRealtimeInternalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/internal/realtime/verify-subscription", VerifySubscriptionAsync)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> VerifySubscriptionAsync(
        VerifySubscriptionRequest request,
        IConfiguration configuration,
        IDiscussionRepository discussionRepository,
        IHubRepository hubRepository,
        ISpaceRepository spaceRepository,
        HttpContext httpContext)
    {
        // Validate API key — this endpoint is internal only (Snakk.Realtime → Snakk.Api)
        var expectedKey = configuration["Realtime:ApiKey"];
        if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != expectedKey)
            return Results.StatusCode(StatusCodes.Status401Unauthorized);

        var exists = request.ScopeType switch
        {
            "Discussion" => await discussionRepository.GetByPublicIdAsync(request.ScopeId) is not null,
            "Space"      => await spaceRepository.GetByPublicIdAsync(request.ScopeId) is not null,
            "Hub"        => await hubRepository.GetByPublicIdAsync(request.ScopeId) is not null,
            _            => false
        };

        return exists
            ? Results.Ok()
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}

public record VerifySubscriptionRequest(string UserId, string ScopeType, string ScopeId);
