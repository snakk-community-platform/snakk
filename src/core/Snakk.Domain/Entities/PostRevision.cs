namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;

public class PostRevision
{
    public PostId PostId { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserId EditedByUserId { get; private set; }
    public int RevisionNumber { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private PostRevision()
    {
    }
#pragma warning restore CS8618

    private PostRevision(
        PostId postId,
        string content,
        UserId editedByUserId,
        int revisionNumber,
        DateTime createdAt)
    {
        PostId = postId;
        Content = content;
        EditedByUserId = editedByUserId;
        RevisionNumber = revisionNumber;
        CreatedAt = createdAt;
    }

    public static PostRevision Create(
        PostId postId,
        string content,
        UserId editedByUserId,
        int revisionNumber)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Revision content cannot be empty", nameof(content));

        return new PostRevision(
            postId,
            content,
            editedByUserId,
            revisionNumber,
            DateTime.UtcNow);
    }

    public static PostRevision Rehydrate(
        PostId postId,
        string content,
        UserId editedByUserId,
        int revisionNumber,
        DateTime createdAt)
    {
        return new PostRevision(
            postId,
            content,
            editedByUserId,
            revisionNumber,
            createdAt);
    }
}
