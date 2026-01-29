namespace Snakk.Domain.ValueObjects;

public record FollowId
{
    public string Value { get; }

    private FollowId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("FollowId cannot be empty", nameof(value));

        Value = value;
    }

    public static FollowId From(string value) => new(value);
    public static FollowId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(FollowId id) => id.Value;
}
