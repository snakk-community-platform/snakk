using Grpc.Core;
using Snakk.Protos.Passkey;

namespace Snakk.Auth.Endpoints;

public static class PasskeyLoginEndpoints
{
    public static void MapPasskeyLoginEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/passkey");

        group.MapPost("/begin-login", async (
            BeginLoginBody? body,
            PasskeyService.PasskeyServiceClient passkeyClient,
            CancellationToken ct) =>
        {
            try
            {
                var request = new BeginPasskeyLoginRequest();
                if (!string.IsNullOrWhiteSpace(body?.Email))
                    request.Email = body.Email;

                var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: ct);
                var response = await passkeyClient.BeginPasskeyLoginAsync(request, callOptions);
                return Results.Ok(new { optionsJson = response.OptionsJson, challengeId = response.ChallengeId });
            }
            catch (RpcException ex)
            {
                return Results.Problem(ex.Status.Detail, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        group.MapPost("/complete-login", async (
            CompleteLoginBody body,
            PasskeyService.PasskeyServiceClient passkeyClient,
            Snakk.Protos.Consent.ConsentService.ConsentServiceClient consentClient,
            HttpContext ctx,
            ILogger<WebApplication> logger,
            CancellationToken ct) =>
        {
            try
            {
                var grpcRequest = new CompletePasskeyLoginRequest
                {
                    ChallengeId = body.ChallengeId,
                    AssertionResponseJson = body.AssertionResponseJson,
                    IpAddress = ctx.Connection.RemoteIpAddress?.ToString() ?? "",
                    UserAgent = ctx.Request.Headers.UserAgent.ToString()
                };

                var response = await passkeyClient.CompletePasskeyLoginAsync(grpcRequest, cancellationToken: ct);

                if (response.TwoFactorRequired)
                    return Results.Ok(new { twoFactorRequired = true, email = response.Email });

                var expiry = DateTimeOffset.UtcNow.AddHours(8);
                var strictOptions = new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = expiry
                };
                var laxOptions = new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/", Expires = expiry
                };

                ctx.Response.Cookies.Append(".Snakk.Auth", response.AccessToken, strictOptions);
                ctx.Response.Cookies.Append(".Snakk.Auth.Session", response.AccessToken, laxOptions);
                if (!string.IsNullOrEmpty(response.RefreshToken))
                    ctx.Response.Cookies.Append(".Snakk.Auth.Refresh", response.RefreshToken, strictOptions);

                var returnUrl = body.ReturnUrl;
                if (string.IsNullOrEmpty(returnUrl) || !IsLocalUrl(returnUrl))
                    returnUrl = "/";

                // Check for pending consents
                try
                {
                    var consentHeaders = new Metadata { { "authorization", $"Bearer {response.AccessToken}" } };
                    var pending = await consentClient.GetPendingConsentsAsync(
                        new Snakk.Protos.Consent.GetPendingConsentsRequest(),
                        headers: consentHeaders,
                        cancellationToken: ct);

                    if (pending.Consents.Count > 0)
                        returnUrl = $"/auth/consent?returnUrl={Uri.EscapeDataString(returnUrl)}";
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to check consent status after passkey login");
                }

                return Results.Ok(new { redirectUrl = returnUrl });
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
            {
                return Results.Json(new { error = "Passkey verification failed. Please try again." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (RpcException ex)
            {
                logger.LogError(ex, "Passkey complete-login gRPC error: {Detail}", ex.Status.Detail);
                return Results.Problem("An error occurred during passkey login.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }

    private static bool IsLocalUrl(string url) =>
        url.StartsWith('/') && !url.StartsWith("//");

    private record BeginLoginBody(string? Email);
    private record CompleteLoginBody(string ChallengeId, string AssertionResponseJson, string? ReturnUrl);
}
