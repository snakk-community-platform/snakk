using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Consent;

namespace Snakk.Auth.Pages;

public class ConsentModel(
    ConsentService.ConsentServiceClient consentClient,
    ILogger<ConsentModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public List<PendingConsentInfo> PendingConsents { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // The consent page needs the user's JWT to call gRPC.
        // Read it from the auth cookie.
        var accessToken = Request.Cookies[".Snakk.Auth"];
        if (string.IsNullOrEmpty(accessToken))
            return RedirectToPage("/Login");

        try
        {
            var headers = new Grpc.Core.Metadata { { "authorization", $"Bearer {accessToken}" } };
            var response = await consentClient.GetPendingConsentsAsync(
                new GetPendingConsentsRequest(), headers: headers);

            PendingConsents = response.Consents.ToList();

            if (PendingConsents.Count == 0)
                return Redirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load pending consents");
            return Redirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var accessToken = Request.Cookies[".Snakk.Auth"];
        if (string.IsNullOrEmpty(accessToken))
            return RedirectToPage("/Login");

        // Collect all version IDs from the form
        var versionIds = Request.Form["versionId"]
            .Select(v => int.TryParse(v, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

        // Verify all checkboxes were checked
        var acceptedSlugs = Request.Form["accepted"].ToHashSet();

        try
        {
            var headers = new Grpc.Core.Metadata { { "authorization", $"Bearer {accessToken}" } };

            // Re-fetch pending to validate
            var pending = await consentClient.GetPendingConsentsAsync(
                new GetPendingConsentsRequest(), headers: headers);

            var allAccepted = pending.Consents.All(c => acceptedSlugs.Contains(c.Slug));
            if (!allAccepted)
            {
                ErrorMessage = "You must accept all required agreements to continue.";
                PendingConsents = pending.Consents.ToList();
                return Page();
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            await consentClient.AcceptConsentsAsync(
                new AcceptConsentsRequest
                {
                    IpAddress = ipAddress,
                    VersionIds = { pending.Consents.Select(c => c.VersionId) }
                }, headers: headers);

            return Redirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to accept consents");
            ErrorMessage = "An error occurred. Please try again.";
            return Page();
        }
    }
}
