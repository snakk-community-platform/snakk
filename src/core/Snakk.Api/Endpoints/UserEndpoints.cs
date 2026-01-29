namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Infrastructure.Database;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapGet("/search", SearchUsersAsync)
            .WithName("SearchUsers");

        group.MapGet("/top-contributors-today", GetTopContributorsTodayAsync)
            .WithName("GetTopContributorsToday");

        group.MapGet("/{publicId}/profile", GetUserProfileAsync)
            .WithName("GetUserProfile");
    }

    private static async Task<IResult> GetUserProfileAsync(
        string publicId,
        UserProfileUseCase userProfileUseCase)
    {
        var profile = await userProfileUseCase.GetUserProfileAsync(publicId);
        if (profile == null)
            return Results.NotFound();

        return Results.Ok(profile);
    }

    private static async Task<IResult> SearchUsersAsync(
        string query,
        int? limit,
        IUserRepository userRepository)
    {
        var users = await userRepository.SearchByDisplayNameAsync(query, limit ?? 5);

        return Results.Ok(users.Select(u => new
        {
            publicId = u.PublicId.Value,
            displayName = u.DisplayName,
            avatarUrl = $"/avatars/{u.PublicId.Value}"
        }));
    }

    private static async Task<IResult> GetTopContributorsTodayAsync(
        SnakkDbContext dbContext,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null)
    {
        var today = DateTime.UtcNow.Date;

        var postsQuery = dbContext.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.CreatedAt >= today);

        // Filter by community if specified
        if (!string.IsNullOrEmpty(communityId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.Community.PublicId == communityId);
        }

        // Filter by space if specified (most specific)
        if (!string.IsNullOrEmpty(spaceId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.PublicId == spaceId);
        }
        // Filter by hub if specified
        else if (!string.IsNullOrEmpty(hubId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.PublicId == hubId);
        }

        var topContributors = await postsQuery
            .GroupBy(p => p.CreatedByUserId)
            .Select(g => new { UserId = g.Key, PostCountToday = g.Count() })
            .OrderByDescending(x => x.PostCountToday)
            .Take(5)
            .Join(
                dbContext.Users.AsNoTracking().Where(u => !u.IsDeleted),
                x => x.UserId,
                u => u.Id,
                (x, u) => new
                {
                    publicId = u.PublicId,
                    displayName = u.DisplayName,
                    postCountToday = x.PostCountToday
                })
            .ToListAsync();

        return Results.Ok(new { items = topContributors });
    }
}
