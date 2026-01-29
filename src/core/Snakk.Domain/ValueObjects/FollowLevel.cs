namespace Snakk.Domain.ValueObjects;

/// <summary>
/// Defines the notification level for a follow.
/// Only applicable to Space follows (Discussion follows always include posts).
/// </summary>
public enum FollowLevel
{
    /// <summary>
    /// Notify only when new discussions are created in the followed space.
    /// </summary>
    DiscussionsOnly,

    /// <summary>
    /// Notify for new discussions AND all new posts in discussions within the space.
    /// </summary>
    DiscussionsAndPosts
}
