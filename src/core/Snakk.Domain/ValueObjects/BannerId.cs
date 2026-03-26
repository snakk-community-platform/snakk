namespace Snakk.Domain.ValueObjects;

public record BannerId
{
    public string Value { get; }

    private BannerId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("BannerId cannot be empty", nameof(value));

        Value = value;
    }

    public static BannerId From(string value) => new(value);
    public static BannerId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(BannerId id) => id.Value;
}
