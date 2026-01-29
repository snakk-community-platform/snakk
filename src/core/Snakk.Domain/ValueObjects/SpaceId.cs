namespace Snakk.Domain.ValueObjects;

public record SpaceId
{
    public string Value { get; }

    private SpaceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SpaceId cannot be empty", nameof(value));

        Value = value;
    }

    public static SpaceId From(string value) => new(value);
    public static SpaceId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(SpaceId id) => id.Value;
}
