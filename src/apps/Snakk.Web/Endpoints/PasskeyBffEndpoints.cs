namespace Snakk.Web.Endpoints;

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Passkey;
using Snakk.Web.Services;

public static class PasskeyBffEndpoints
{
    public static void MapPasskeyBffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff/auth/passkeys")
            .RequireAuthorization();

        group.MapGet("/", ListPasskeysAsync).WithName("BffListPasskeys");
        group.MapPost("/begin-registration", BeginRegistrationAsync).WithName("BffBeginPasskeyRegistration");
        group.MapPost("/complete-registration", CompleteRegistrationAsync).WithName("BffCompletePasskeyRegistration");
        group.MapDelete("/{id:int}", DeletePasskeyAsync).WithName("BffDeletePasskey");
    }

    private static async Task<IResult> ListPasskeysAsync(
        HttpContext ctx,
        PasskeyService.PasskeyServiceClient passkeyClient,
        CancellationToken ct)
    {
        var publicId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(publicId)) return Results.Unauthorized();

        try
        {
            var response = await passkeyClient.GetUserPasskeysAsync(
                new GetUserPasskeysRequest { UserPublicId = publicId }, cancellationToken: ct);

            var passkeys = response.Passkeys.Select(p => new
            {
                id = p.Id,
                friendlyName = p.FriendlyName,
                transports = p.Transports,
                createdAt = p.CreatedAt.ToDateTimeOffset(),
                lastUsedAt = p.LastUsedAt != null ? p.LastUsedAt.ToDateTimeOffset() : (DateTimeOffset?)null
            });

            return Results.Ok(passkeys);
        }
        catch (RpcException ex)
        {
            return Results.Problem(ex.Status.Detail, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> BeginRegistrationAsync(
        CredentialVerificationBody? body,
        HttpContext ctx,
        PasskeyService.PasskeyServiceClient passkeyClient,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var publicId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var displayName = ctx.User.FindFirstValue(ClaimTypes.Name) ?? publicId ?? "";
        if (string.IsNullOrEmpty(publicId)) return Results.Unauthorized();

        var credError = await VerifyCredentialAsync(ctx, httpClientFactory, body?.Password, body?.TotpCode, ct);
        if (credError is not null) return credError;

        try
        {
            var response = await passkeyClient.BeginPasskeyRegistrationAsync(
                new BeginPasskeyRegistrationRequest
                {
                    UserPublicId = publicId,
                    UserDisplayName = displayName
                }, cancellationToken: ct);

            return Results.Ok(new { optionsJson = response.OptionsJson, challengeId = response.ChallengeId });
        }
        catch (RpcException ex)
        {
            return Results.Problem(ex.Status.Detail, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CompleteRegistrationAsync(
        CompleteRegistrationBody body,
        HttpContext ctx,
        PasskeyService.PasskeyServiceClient passkeyClient,
        CancellationToken ct)
    {
        var publicId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(publicId)) return Results.Unauthorized();

        try
        {
            var request = new CompletePasskeyRegistrationRequest
            {
                UserPublicId = publicId,
                ChallengeId = body.ChallengeId,
                AttestationResponseJson = body.AttestationResponseJson
            };
            if (!string.IsNullOrEmpty(body.FriendlyName))
                request.FriendlyName = body.FriendlyName;

            await passkeyClient.CompletePasskeyRegistrationAsync(request, cancellationToken: ct);
            return Results.NoContent();
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

    private static async Task<IResult> DeletePasskeyAsync(
        int id,
        [FromBody] CredentialVerificationBody? body,
        HttpContext ctx,
        PasskeyService.PasskeyServiceClient passkeyClient,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var publicId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(publicId)) return Results.Unauthorized();

        var credError = await VerifyCredentialAsync(ctx, httpClientFactory, body?.Password, body?.TotpCode, ct);
        if (credError is not null) return credError;

        try
        {
            await passkeyClient.DeletePasskeyAsync(
                new DeletePasskeyRequest { UserPublicId = publicId, CredentialId = id },
                cancellationToken: ct);
            return Results.NoContent();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return Results.NotFound();
        }
        catch (RpcException ex)
        {
            return Results.Problem(ex.Status.Detail, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult?> VerifyCredentialAsync(
        HttpContext ctx,
        IHttpClientFactory httpClientFactory,
        string? password,
        string? totpCode,
        CancellationToken ct)
    {
        var sudoToken = ctx.Request.Cookies[AuthCookieHelper.SudoCookieName];
        if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(totpCode) && string.IsNullOrWhiteSpace(sudoToken))
            return Results.BadRequest(new { error = "Authentication required." });

        var accessToken = ctx.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/verify-credential");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var payload = JsonSerializer.Serialize(new { password, totpCode, sudoToken });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }

    private record CredentialVerificationBody(string? Password, string? TotpCode);
    private record CompleteRegistrationBody(string ChallengeId, string AttestationResponseJson, string? FriendlyName);
}
