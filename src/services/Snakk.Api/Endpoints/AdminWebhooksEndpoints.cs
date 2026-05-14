using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Snakk.Api.Services;
using Snakk.Application.DTOs.Webhooks;
using Snakk.Application.Services;

namespace Snakk.Api.Endpoints;

public static class AdminWebhooksEndpoints
{
    public static void MapAdminWebhooksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/webhooks")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Bearer" })
            .RequireAuthorization(policy => policy.RequireRole("GlobalAdmin"))
            .WithTags("Admin - Webhooks");

        // Get all webhooks
        group.MapGet("/", GetAllWebhooks)
            .WithName("GetAllWebhooks")
            .WithSummary("Get all configured webhooks")
            .Produces<List<WebhookResponse>>();

        // Get webhook by ID
        group.MapGet("/{webhookId:guid}", GetWebhookById)
            .WithName("GetWebhookById")
            .WithSummary("Get a specific webhook by ID")
            .Produces<WebhookResponse>()
            .Produces(404);

        // Create webhook
        group.MapPost("/", CreateWebhook)
            .WithName("CreateWebhook")
            .WithSummary("Create a new webhook")
            .Produces<WebhookResponse>(201)
            .Produces(400);

        // Update webhook
        group.MapPut("/{webhookId:guid}", UpdateWebhook)
            .WithName("UpdateWebhook")
            .WithSummary("Update an existing webhook")
            .Produces<WebhookResponse>()
            .Produces(400)
            .Produces(404);

        // Delete webhook
        group.MapDelete("/{webhookId:guid}", DeleteWebhook)
            .WithName("DeleteWebhook")
            .WithSummary("Delete a webhook")
            .Produces(204)
            .Produces(404);

        // Test webhook
        group.MapPost("/{webhookId:guid}/test", TestWebhook)
            .WithName("TestWebhook")
            .WithSummary("Send a test event to a webhook")
            .Produces<WebhookDeliveryLogResponse>()
            .Produces(400)
            .Produces(404);

        // Get webhook delivery logs
        group.MapGet("/{webhookId:guid}/logs", GetWebhookDeliveryLogs)
            .WithName("GetWebhookDeliveryLogs")
            .WithSummary("Get delivery logs for a webhook")
            .Produces<List<WebhookDeliveryLogResponse>>()
            .Produces(404);

        // Get available event types
        group.MapGet("/events/available", GetAvailableEventTypes)
            .WithName("GetAvailableEventTypes")
            .WithSummary("Get all available webhook event types")
            .Produces<List<WebhookEventInfo>>();
    }

    private static async Task<IResult> GetAllWebhooks(
        [FromServices] IWebhookService webhookService,
        CancellationToken cancellationToken)
    {
        var webhooks = await webhookService.GetAllWebhooksAsync(cancellationToken);
        return Results.Ok(webhooks);
    }

    private static async Task<IResult> GetWebhookById(
        [FromRoute] Guid webhookId,
        [FromServices] IWebhookService webhookService,
        CancellationToken cancellationToken)
    {
        var webhook = await webhookService.GetWebhookByIdAsync(webhookId, cancellationToken);

        if (webhook is null)
            return Results.NotFound(new { error = "Webhook not found" });

        return Results.Ok(webhook);
    }

    private static async Task<IResult> CreateWebhook(
        [FromBody] CreateWebhookRequest request,
        [FromServices] IWebhookService webhookService,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserService.GetCurrentUserId();

        if (currentUser is null)
            return Results.Unauthorized();

        var webhook = await webhookService.CreateWebhookAsync(request, currentUser, cancellationToken);

        return Results.Created($"/admin/webhooks/{webhook.Id}", webhook);
    }

    private static async Task<IResult> UpdateWebhook(
        [FromRoute] Guid webhookId,
        [FromBody] UpdateWebhookRequest request,
        [FromServices] IWebhookService webhookService,
        CancellationToken cancellationToken)
    {
        var webhook = await webhookService.UpdateWebhookAsync(webhookId, request, cancellationToken);

        if (webhook is null)
            return Results.NotFound(new { error = "Webhook not found" });

        return Results.Ok(webhook);
    }

    private static async Task<IResult> DeleteWebhook(
        [FromRoute] Guid webhookId,
        [FromServices] IWebhookService webhookService,
        CancellationToken cancellationToken)
    {
        var deleted = await webhookService.DeleteWebhookAsync(webhookId, cancellationToken);

        if (!deleted)
            return Results.NotFound(new { error = "Webhook not found" });

        return Results.NoContent();
    }

    private static async Task<IResult> TestWebhook(
        [FromRoute] Guid webhookId,
        [FromBody] WebhookTestRequest request,
        [FromServices] IWebhookService webhookService,
        CancellationToken cancellationToken)
    {
        try
        {
            var deliveryLog = await webhookService.TestWebhookAsync(webhookId, request, cancellationToken);
            return Results.Ok(deliveryLog);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound(new { error = "Webhook not found." });
        }
        catch (Exception)
        {
            return Results.BadRequest(new { error = "An unexpected error occurred." });
        }
    }

    private static async Task<IResult> GetWebhookDeliveryLogs(
        [FromRoute] Guid webhookId,
        [FromServices] IWebhookService webhookService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 50, 1, 100);
        page = Math.Max(1, page);

        var logs = await webhookService.GetDeliveryLogsAsync(webhookId, page, pageSize, cancellationToken);

        return Results.Ok(logs);
    }

    private static async Task<IResult> GetAvailableEventTypes(
        [FromServices] IWebhookService webhookService,
        CancellationToken cancellationToken)
    {
        var events = await webhookService.GetAvailableEventTypesAsync(cancellationToken);
        return Results.Ok(events);
    }
}
