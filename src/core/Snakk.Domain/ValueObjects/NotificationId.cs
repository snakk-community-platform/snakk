namespace Snakk.Domain.ValueObjects;

public record NotificationId
{
    public string Value { get; }

    private NotificationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("NotificationId cannot be empty", nameof(value));

        Value = value;
    }

    public static NotificationId From(string value) => new(value);
    public static NotificationId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(NotificationId id) => id.Value;
}
