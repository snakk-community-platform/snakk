namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", GetNotificationsAsync)
            .WithName("GetNotifications");

        group.MapGet("/unread-count", GetUnreadCountAsync)
            .WithName("GetUnreadNotificationCount")
            .Produces<UnreadCountResponse>();

        group.MapPost("/{notificationId}/read", MarkAsReadAsync)
            .WithName("MarkNotificationAsRead")
            .Produces<SuccessResponse>();

        group.MapPost("/read-all", MarkAllAsReadAsync)
            .WithName("MarkAllNotificationsAsRead")
            .Produces<SuccessResponse>();
    }

    private static async Task<IResult> GetNotificationsAsync(
        int offset,
        int pageSize,
        HttpContext httpContext,
        NotificationUseCase notificationUseCase)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var result = await notificationUseCase.GetNotificationsAsync(
            UserId.From(userIdClaim.Value),
            offset,
            pageSize);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetUnreadCountAsync(
        HttpContext httpContext,
        NotificationUseCase notificationUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var count = await notificationUseCase.GetUnreadCountAsync(UserId.From(userIdClaim.Value));

        return TypedResults.Ok(new UnreadCountResponse(count));
    }

    private static async Task<IResult> MarkAsReadAsync(
        string notificationId,
        HttpContext httpContext,
        NotificationUseCase notificationUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var result = await notificationUseCase.MarkAsReadAsync(
            NotificationId.From(notificationId),
            UserId.From(userIdClaim.Value));

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Ok(new SuccessResponse(true));
    }

    private static async Task<IResult> MarkAllAsReadAsync(
        HttpContext httpContext,
        NotificationUseCase notificationUseCase)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        await notificationUseCase.MarkAllAsReadAsync(UserId.From(userIdClaim.Value));

        return TypedResults.Ok(new SuccessResponse(true));
    }
}
