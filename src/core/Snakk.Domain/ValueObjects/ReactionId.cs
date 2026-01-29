namespace Snakk.Domain.ValueObjects;

public record ReactionId
{
    public string Value { get; }

    private ReactionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ReactionId cannot be empty", nameof(value));

        Value = value;
    }

    public static ReactionId From(string value) => new(value);
    public static ReactionId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(ReactionId id) => id.Value;
}
