namespace Snakk.Domain.ValueObjects;

public record PostId
{
    public string Value { get; }

    private PostId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("PostId cannot be empty", nameof(value));

        Value = value;
    }

    public static PostId From(string value) => new(value);
    public static PostId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(PostId id) => id.Value;
}
