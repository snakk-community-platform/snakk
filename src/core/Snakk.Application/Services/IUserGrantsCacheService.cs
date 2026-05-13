namespace Snakk.Application.Services;

public interface IUserGrantsCacheService
{
    /// <summary>
    /// Returns the resolved CanRead grants for the given user.
    /// Results are cached per user with a configurable TTL (default 5 min).
    /// </summary>
    Task<UserGrants> GetGrantsAsync(string userId);

    /// <summary>
    /// Returns the set of all restricted entity IDs across the platform, grouped by type.
    /// Cached with a short configurable TTL (default 30 sec).
    /// Use <see cref="RestrictedEntitySet.IsEmpty"/> as a platform-level short-circuit.
    /// </summary>
    Task<RestrictedEntitySet> GetRestrictedEntitiesAsync();

    /// <summary>
    /// Returns true if any entity in the platform has IsRestricted = true.
    /// Delegates to <see cref="GetRestrictedEntitiesAsync"/> — result shares the same cache.
    /// </summary>
    Task<bool> AnyRestrictedAsync();

    /// <summary>Evicts the grant cache for a specific user (e.g. when their group membership changes).</summary>
    void Invalidate(string userId);

    /// <summary>
    /// Evicts grant caches for all users by advancing the internal generation counter.
    /// Used when a GroupAccess row changes, affecting all members of that group.
    /// </summary>
    void InvalidateAll();

    /// <summary>Evicts the restricted-entity set cache (e.g. when IsRestricted changes on any entity).</summary>
    void InvalidateRestrictedCount();

    /// <summary>
    /// Returns the set of Space IDs whose community has HideAdultDiscussionsFromLists = true.
    /// Cached with a short TTL; invalidated whenever that community setting changes.
    /// </summary>
    Task<HashSet<int>> GetAdultHidingSpaceIdsAsync();

    /// <summary>Evicts the adult-hiding-spaces cache (e.g. when a community's HideAdultDiscussionsFromLists changes).</summary>
    void InvalidateAdultHidingSpaces();
}

/// <summary>
/// Resolved CanRead grants for a user, keyed by internal integer IDs.
/// Used by access-filter helpers in repositories to replace correlated EXISTS
/// subqueries with a simple <c>= ANY(@array)</c> lookup.
/// </summary>
public record UserGrants(
    HashSet<int> SpaceIds,
    HashSet<int> HubIds,
    HashSet<int> CommunityIds);

/// <summary>
/// Platform-wide set of entity IDs that have IsRestricted = true, grouped by type.
/// Cached globally and used to short-circuit access checks at the individual-entity
/// and list-query level.
/// </summary>
public record RestrictedEntitySet(
    HashSet<int> SpaceIds,
    HashSet<int> HubIds,
    HashSet<int> CommunityIds)
{
    public static readonly RestrictedEntitySet Empty =
        new([], [], []);

    public bool IsEmpty =>
        SpaceIds.Count == 0 && HubIds.Count == 0 && CommunityIds.Count == 0;
}
