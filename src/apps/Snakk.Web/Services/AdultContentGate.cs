using System.Security.Claims;

namespace Snakk.Web.Services;

/// <summary>
/// Checks whether the current user has accepted adult content viewing.
/// Returns true if the user can view adult content (no gate needed).
/// </summary>
public static class AdultContentGate
{
    /// <summary>
    /// Returns true if the user is allowed to view adult-only content.
    /// Either: user opted in via settings, or session cookie from interstitial.
    /// </summary>
    public static bool IsAllowed(HttpContext context)
    {
        // Check session cookie (set by interstitial confirmation)
        if (context.Request.Cookies.ContainsKey("snakk.adult-confirmed"))
            return true;

        // Check user preference (AllowAdultContent in JWT claims would be ideal,
        // but we don't have it as a claim — so pages should pass this from the gRPC response)
        return false;
    }
}
