namespace Snakk.Domain.ValueObjects;

public record AdminUserId
{
    public string Value { get; }

    private AdminUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AdminUserId cannot be empty", nameof(value));

        Value = value;
    }

    public static AdminUserId From(string value) => new(value);
    public static AdminUserId New() => new($"a_{Ulid.NewUlid().ToString()}");

    public override string ToString() => Value;

    public static implicit operator string(AdminUserId id) => id.Value;
}
