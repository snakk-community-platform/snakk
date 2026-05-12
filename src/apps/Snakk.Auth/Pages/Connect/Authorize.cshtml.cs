namespace Snakk.Auth.Pages.Connect;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using System.Text;

public class AuthorizeModel(IConfiguration configuration) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        // OpenIddict has already validated client_id, redirect_uri, PKCE, scopes, etc.
        // before passing through to this page — no need to re-read the request object.

        var prompt = Request.Query["prompt"].ToString();

        // prompt=login: client requires fresh authentication regardless of session state.
        if (prompt == "login")
            return RedirectToLogin();

        // prompt=none: client requires silent auth — no UI permitted under any circumstances.
        var promptNone = prompt == "none";

        var jwt = Request.Cookies[".Snakk.Auth"];
        if (string.IsNullOrEmpty(jwt))
            return promptNone ? LoginRequired() : RedirectToLogin();

        var secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey not configured");

        var handler = new JsonWebTokenHandler();
        var validation = await handler.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidIssuer = configuration["Jwt:Issuer"] ?? "Snakk",
            ValidAudience = configuration["Jwt:Audience"] ?? "Snakk",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        });

        if (!validation.IsValid)
            return promptNone ? LoginRequired() : RedirectToLogin();

        var sub = validation.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sub))
            return promptNone ? LoginRequired() : RedirectToLogin();

        // Build a minimal OpenIddict principal — only forward safe, public-facing claims.
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, sub);

        var email = validation.ClaimsIdentity.FindFirst(ClaimTypes.Email)?.Value;
        if (email is not null)
            identity.SetClaim(OpenIddictConstants.Claims.Email, email);

        var displayName = validation.ClaimsIdentity.FindFirst(ClaimTypes.Name)?.Value;
        if (displayName is not null)
            identity.SetClaim(OpenIddictConstants.Claims.Name, displayName);

        var principal = new ClaimsPrincipal(identity);

        // Scopes come from the query string; OpenIddict validated them against
        // the registered client before handing off to this passthrough page.
        var requestedScopes = Request.Query["scope"].ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        principal.SetScopes(requestedScopes);
        principal.SetResources("snakk-public-api");

        // SignIn with the OpenIddict scheme → OpenIddict issues the auth code and
        // redirects the browser back to the client's redirect_uri.
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    public IActionResult OnPost() => NotFound();

    private ForbidResult LoginRequired() => Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            ["error"] = OpenIddictConstants.Errors.LoginRequired,
            ["error_description"] = "The user is not authenticated."
        }),
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    private RedirectToPageResult RedirectToLogin()
    {
        // Preserve the full OAuth query string so OpenIddict can resume after login.
        var returnUrl = $"/connect/authorize{Request.QueryString}";
        return RedirectToPage("/Login", new { returnUrl });
    }
}
