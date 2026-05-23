namespace Snakk.Web.Endpoints;

using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Snakk.Protos.Auth;
using Snakk.Web.Services;

public static class OAuthConnectionBffEndpoints
{
    public static void MapOAuthConnectionBffEndpoints(this IEndpointRouteBuilder app)
    {
        static async ValueTask<object?> RequireOAuth(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
        {
            var opts = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<OAuthProvidersOptions>>();
            return opts.Value.HasAny ? await next(ctx) : Results.NotFound();
        }

        var connections = app.MapGroup("/bff/settings/oauth-connections")
            .RequireAuthorization()
            .WithTags("OAuth Connections")
            .AddEndpointFilter(RequireOAuth);

        connections.MapGet("/", GetOAuthConnectionsAsync).WithName("BffGetOAuthConnections");
        connections.MapDelete("/{provider}", DisconnectOAuthProviderAsync).WithName("BffDisconnectOAuthProvider");
        connections.MapPatch("/{provider}/require-2fa", SetRequire2FAAsync).WithName("BffSetOAuthRequire2FA");

        app.MapPost("/bff/auth/sudo/oauth", IssueSudoFromOAuthNonceAsync)
            .RequireAuthorization()
            .WithTags("OAuth Connections")
            .WithName("BffIssueSudoFromOAuth")
            .RequireRateLimiting("flood-post")
            .AddEndpointFilter(RequireOAuth);
    }

    private static async Task<IResult> GetOAuthConnectionsAsync(
        AuthService.AuthServiceClient authClient,
        CancellationToken ct)
    {
        try
        {
            var response = await authClient.GetOAuthConnectionsAsync(
                new GetOAuthConnectionsRequest(), cancellationToken: ct);

            var connections = response.Connections.Select(c => new
            {
                provider = c.Provider,
                connectedAt = c.ConnectedAt.ToDateTimeOffset(),
                lastLoginAt = c.LastLoginAt != null ? c.LastLoginAt.ToDateTimeOffset() : (DateTimeOffset?)null,
                require2FA = c.Require2Fa
            });

            return Results.Ok(connections);
        }
        catch (RpcException ex)
        {
            return Results.Problem(ex.Status.Detail, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> DisconnectOAuthProviderAsync(
        string provider,
        AuthService.AuthServiceClient authClient,
        CancellationToken ct)
    {
        try
        {
            await authClient.DisconnectOAuthProviderAsync(
                new DisconnectOAuthProviderRequest { Provider = provider }, cancellationToken: ct);
            return Results.Ok();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return ex.Status.Detail == "LAST_AUTH_METHOD"
                ? Results.BadRequest(new { error = "LAST_AUTH_METHOD" })
                : Results.BadRequest(new { error = ex.Status.Detail });
        }
        catch (RpcException ex)
        {
            return Results.Problem(ex.Status.Detail, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> SetRequire2FAAsync(
        string provider,
        [FromBody] SetRequire2FABody body,
        AuthService.AuthServiceClient authClient,
        CancellationToken ct)
    {
        try
        {
            await authClient.SetOAuthConnectionRequire2FAAsync(
                new SetOAuthConnectionRequire2FARequest { Provider = provider, Require2Fa = body.Require },
                cancellationToken: ct);
            return Results.Ok();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
        {
            return Results.BadRequest(new { error = ex.Status.Detail });
        }
        catch (RpcException ex)
        {
            return Results.Problem(ex.Status.Detail, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> IssueSudoFromOAuthNonceAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/sudo/oauth");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StreamContent(httpContext.Request.Body);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        var token = root.TryGetProperty("sudoToken", out var tp) ? tp.GetString() : null;
        var expires = root.TryGetProperty("expiresInSeconds", out var ep) ? ep.GetInt32() : 300;

        if (!string.IsNullOrEmpty(token))
            AuthCookieHelper.SetSudoCookie(httpContext, token, expires);

        return Results.Ok(new { expiresInSeconds = expires });
    }

    private record SetRequire2FABody(bool Require);
}
