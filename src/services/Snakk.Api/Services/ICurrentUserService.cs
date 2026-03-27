namespace Snakk.Api.Services;

public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's ID from claims, or null if not authenticated
    /// </summary>
    string? GetCurrentUserId();

    /// <summary>
    /// Gets the current user's display name from claims
    /// </summary>
    string? GetCurrentUserDisplayName();

    /// <summary>
    /// Gets the current user's email from claims
    /// </summary>
    string? GetCurrentUserEmail();

    /// <summary>
    /// Checks if the current user is authenticated
    /// </summary>
    bool IsAuthenticated();

    /// <summary>
    /// Gets the OAuth provider used by current user
    /// </summary>
    string? GetOAuthProvider();

    /// <summary>
    /// Gets whether the current user's email is verified
    /// </summary>
    bool IsEmailVerified();

    /// <summary>
    /// Gets the current user's role (admin, mod, or null)
    /// </summary>
    string? GetCurrentUserRole();

    /// <summary>
    /// Gets the current user's uploaded avatar filename from claims, or null
    /// </summary>
    string? GetAvatarFileName();
}
