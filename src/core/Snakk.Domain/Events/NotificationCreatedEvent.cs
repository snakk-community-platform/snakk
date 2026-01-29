namespace Snakk.Domain.Events;

using Snakk.Domain.ValueObjects;

public record NotificationCreatedEvent(
    NotificationId NotificationId,
    UserId RecipientUserId,
    NotificationType Type) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
