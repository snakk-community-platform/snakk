namespace Snakk.Application.Services;

public record ConsentTypeInfo(
    string Slug,
    string Name,
    string? ShortLabel,
    string? LinkUrl,
    bool IsRequired,
    int DisplayOrder,
    int LatestVersionId,
    int LatestVersionNumber);

public record ConsentVersionInfo(
    int VersionId,
    int VersionNumber,
    string Text,
    DateTime CreatedAt);

public record PendingConsent(
    string Slug,
    string Name,
    string? ShortLabel,
    string? LinkUrl,
    int VersionId);

public interface IConsentService
{
    /// <summary>
    /// Gets all active required consent types with their latest version.
    /// Used during registration and consent interstitial.
    /// </summary>
    Task<List<ConsentTypeInfo>> GetActiveRequiredConsentsAsync();

    /// <summary>
    /// Gets the latest version text for a consent type by slug.
    /// Used for the /legal/{slug} display page.
    /// </summary>
    Task<ConsentVersionInfo?> GetLatestVersionAsync(string slug);

    /// <summary>
    /// Checks which required consents the user is missing (no consent record
    /// for the latest version of each required+active type).
    /// </summary>
    Task<List<PendingConsent>> GetPendingConsentsAsync(string userPublicId);

    /// <summary>
    /// Records that a user accepted a specific consent type version.
    /// </summary>
    Task AcceptConsentAsync(string userPublicId, int consentTypeVersionId, string? ipAddress);

    /// <summary>
    /// Records multiple consents at once (used during registration).
    /// </summary>
    Task AcceptConsentsAsync(string userPublicId, IEnumerable<int> consentTypeVersionIds, string? ipAddress);

    /// <summary>
    /// Returns true if the user has no pending required consents.
    /// </summary>
    Task<bool> HasAllRequiredConsentsAsync(string userPublicId);
}
