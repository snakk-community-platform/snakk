namespace Snakk.Application.Services;

/// <summary>
/// Manages user profile slug lifecycle: generation, uniqueness, history, and validation.
/// </summary>
public interface ISlugService
{
    /// <summary>
    /// Validates that a slug string meets format requirements:
    /// lowercase a-z, 0-9, hyphen; length 3-30; no reserved names.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    string? ValidateSlugFormat(string slug);

    /// <summary>
    /// Checks whether the user's slug change is allowed under the rate-limit rules:
    /// 2 free changes, then max 2 per calendar year (one every ~6 months).
    /// </summary>
    bool IsSlugRateLimited(int changeCount, DateTime? lastChangedAt);

    /// <summary>
    /// Returns a unique slug based on the candidate, appending a numeric suffix
    /// (e.g. "pal-rune1", "pal-rune2") if needed to avoid collisions.
    /// Pass excludeUserId to allow a user to keep their own current slug.
    /// </summary>
    Task<string> EnsureUniqueSlugAsync(string candidateSlug, int? excludeUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Checks if a slug is available (not taken by another user and not in slug history
    /// owned by another user). Pass currentUserPublicId to allow checking their own old slugs.
    /// Returns (available, suggestion) where suggestion is non-null when unavailable.
    /// </summary>
    Task<(bool Available, string? Suggestion)> CheckSlugAvailabilityAsync(string slug, string currentUserPublicId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's slug, saving the old slug to history and handling the reclaim
    /// flow (deletes history entry if the user is reclaiming a slug they previously held).
    /// Does NOT enforce rate limits — callers must check beforehand.
    /// </summary>
    Task UpdateUserSlugAsync(string publicId, string newSlug, bool isCustomized, CancellationToken ct = default);

    /// <summary>
    /// Generates and assigns an initial slug for a new user from their display name.
    /// Safe to call at registration time; resolves collisions automatically.
    /// </summary>
    Task AssignInitialSlugAsync(string publicId, string displayName, CancellationToken ct = default);

    /// <summary>
    /// Auto-updates the slug when the display name changes (only if IsSlugCustomized = false).
    /// Saves the old slug to history. No-ops if the user has a custom slug.
    /// </summary>
    Task AutoUpdateSlugFromDisplayNameAsync(string publicId, string newDisplayName, CancellationToken ct = default);
}
