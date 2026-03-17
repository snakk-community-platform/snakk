using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface IRuleService
{
    /// <summary>
    /// Get rules for a specific scope. scopeType: "Site", "Community", "Hub", "Space".
    /// For "Site" scope, scopePublicId is ignored.
    /// </summary>
    Task<RulesDto> GetRulesAsync(string scopeType, string? scopePublicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace all rules for a specific scope. Returns the updated rules.
    /// </summary>
    Task<RulesDto> UpdateRulesAsync(
        string scopeType,
        string? scopePublicId,
        UpdateRulesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check whether site-wide rules exist (for sidebar conditional display).
    /// </summary>
    Task<bool> HasSiteRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current revision string for site-wide rules (used for cache-busting).
    /// Returns empty string if no revision is set.
    /// </summary>
    Task<string> GetSiteRulesRevisionAsync(CancellationToken cancellationToken = default);
}
