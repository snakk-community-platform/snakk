namespace Snakk.Domain.ValueObjects;

public record UserId
{
    public string Value { get; }

    private UserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("UserId cannot be empty", nameof(value));

        Value = value;
    }

    public static UserId From(string value) => new(value);
    public static UserId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(UserId id) => id.Value;
}
