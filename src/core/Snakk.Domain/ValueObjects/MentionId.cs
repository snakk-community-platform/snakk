namespace Snakk.Domain.ValueObjects;

public record MentionId
{
    public string Value { get; }

    private MentionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("MentionId cannot be empty", nameof(value));

        Value = value;
    }

    public static MentionId From(string value) => new(value);
    public static MentionId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(MentionId id) => id.Value;
}
