namespace Snakk.Domain.ValueObjects;

public record HubId
{
    public string Value { get; }

    private HubId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("HubId cannot be empty", nameof(value));

        Value = value;
    }

    public static HubId From(string value) => new(value);
    public static HubId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(HubId id) => id.Value;
}
