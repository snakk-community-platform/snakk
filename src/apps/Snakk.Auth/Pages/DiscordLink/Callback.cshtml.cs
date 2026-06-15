using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Auth;
using System.Security.Claims;

namespace Snakk.Auth.Pages.DiscordLink;

public class CallbackModel(
    AuthService.AuthServiceClient authClient,
    ILogger<CallbackModel> logger) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("DiscordLink");
            if (!authenticateResult.Succeeded)
            {
                logger.LogWarning("Discord link OAuth failed: {Reason}",
                    authenticateResult.Failure?.Message ?? "NoResult");
                return Redirect("/discord-link/error?reason=oauth_failed");
            }

            var claims = authenticateResult.Principal?.Claims;
            var discordUserId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var discordUsername = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            // Avatar hash claim — may vary by library version; we treat it as optional
            var avatarHash = claims?.FirstOrDefault(c =>
                c.Type is "urn:discord:avatar:hash" or "urn:discord:avatar")?.Value;

            if (string.IsNullOrEmpty(discordUserId))
                return Redirect("/discord-link/error?reason=missing_claims");

            var linkToken = HttpContext.Session.GetString("DiscordLink_Token");
            HttpContext.Session.Remove("DiscordLink_Token");

            if (string.IsNullOrEmpty(linkToken))
                return Redirect("/discord-link/error?reason=token_missing");

            var req = new CompleteDiscordLinkRequest
            {
                LinkToken = linkToken,
                DiscordUserId = discordUserId,
                DiscordUsername = discordUsername ?? discordUserId
            };
            if (!string.IsNullOrEmpty(avatarHash))
                req.DiscordAvatarHash = avatarHash;

            var response = await authClient.CompleteDiscordLinkAsync(req);

            if (!response.Success)
            {
                var reason = response.HasErrorCode ? response.ErrorCode.ToLowerInvariant() : "unknown";
                return Redirect($"/discord-link/error?reason={reason}");
            }

            // Snakk.Auth is at /auth, Snakk.Web is at the root
            return Redirect("/my/settings/integrations?discord=linked");
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Discord link gRPC error: {Status}", ex.Status.Detail);
            return Redirect("/discord-link/error?reason=server_error");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discord link callback error");
            return Redirect("/discord-link/error?reason=unknown");
        }
    }
}
