namespace Snakk.Domain.ValueObjects;

public record GroupId
{
    public string Value { get; }

    private GroupId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("GroupId cannot be empty", nameof(value));

        Value = value;
    }

    public static GroupId From(string value) => new(value);
    public static GroupId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(GroupId id) => id.Value;
}
