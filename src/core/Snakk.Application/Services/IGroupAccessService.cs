using Snakk.Application.DTOs.Management;

namespace Snakk.Application.Services;

public interface IGroupAccessService
{
    /// <summary>
    /// Checks what a user can do at the given entity level.
    /// Applies the intersection-gate model: each restricted level in the hierarchy
    /// is an independent gate; all must be passed. Within each gate, read/write
    /// permissions are OR'd across all satisfied grants.
    /// Moderators and admins bypass all group restrictions.
    /// </summary>
    Task<GroupAccessResult> CheckAccessAsync(
        string? userPublicId,
        string communityPublicId,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current IsRestricted flag and group grants for the given scoped entity.
    /// Exactly one of communityPublicId/hubPublicId/spacePublicId should be provided.
    /// </summary>
    Task<EntityAccessDto> GetEntityAccessAsync(
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sets the IsRestricted flag on the given scoped entity.
    /// Exactly one of communityPublicId/hubPublicId/spacePublicId should be provided.
    /// </summary>
    Task SetIsRestrictedAsync(
        bool isRestricted,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a group grant for the given scoped entity.
    /// Exactly one of communityPublicId/hubPublicId/spacePublicId should be provided.
    /// </summary>
    Task UpsertGrantAsync(
        UpsertGroupGrantRequest request,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a group grant from the given scoped entity.
    /// </summary>
    Task<bool> DeleteGrantAsync(
        string groupPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);
}
