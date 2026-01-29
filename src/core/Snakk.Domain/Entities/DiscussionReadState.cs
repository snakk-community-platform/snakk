namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

/// <summary>
/// Tracks the last-read post per user per discussion.
/// Used for "jump to unread" and unread indicators.
/// </summary>
public class DiscussionReadState
{
    public UserId UserId { get; private set; }
    public DiscussionId DiscussionId { get; private set; }
    public PostId? LastReadPostId { get; private set; }
    public DateTime LastReadAt { get; private set; }

#pragma warning disable CS8618
    private DiscussionReadState() { }
#pragma warning restore CS8618

    private DiscussionReadState(
        UserId userId,
        DiscussionId discussionId,
        PostId? lastReadPostId,
        DateTime lastReadAt)
    {
        UserId = userId;
        DiscussionId = discussionId;
        LastReadPostId = lastReadPostId;
        LastReadAt = lastReadAt;
    }

    public static DiscussionReadState Create(
        UserId userId,
        DiscussionId discussionId,
        PostId? lastReadPostId = null)
    {
        return new DiscussionReadState(
            userId,
            discussionId,
            lastReadPostId,
            DateTime.UtcNow);
    }

    public static DiscussionReadState Rehydrate(
        UserId userId,
        DiscussionId discussionId,
        PostId? lastReadPostId,
        DateTime lastReadAt)
    {
        return new DiscussionReadState(
            userId,
            discussionId,
            lastReadPostId,
            lastReadAt);
    }

    public void MarkAsRead(PostId postId)
    {
        LastReadPostId = postId;
        LastReadAt = DateTime.UtcNow;
    }
}
