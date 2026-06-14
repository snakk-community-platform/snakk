namespace Snakk.Application.Services;

/// <summary>
/// Read-only access-check queries on posts, used by API endpoints that
/// must not take a direct dependency on SnakkDbContext.
/// </summary>
public interface IPostAccessDataService
{
    /// <summary>
    /// Returns whether the post's ancestor chain (Space → Hub → Community) has any
    /// IsRestricted flag set, or <c>null</c> when the post does not exist.
    /// </summary>
    Task<bool?> IsPostRestrictedAsync(string publicId, CancellationToken ct = default);
}
