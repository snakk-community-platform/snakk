namespace Snakk.Domain.ValueObjects;

public record AnnouncementId
{
    public string Value { get; }

    private AnnouncementId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AnnouncementId cannot be empty", nameof(value));

        Value = value;
    }

    public static AnnouncementId From(string value) => new(value);
    public static AnnouncementId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(AnnouncementId id) => id.Value;
}
