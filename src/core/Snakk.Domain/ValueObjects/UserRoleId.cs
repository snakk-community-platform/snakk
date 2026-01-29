namespace Snakk.Domain.ValueObjects;

public record UserRoleId
{
    public string Value { get; }

    private UserRoleId(string value)
    {
        Value = value;
    }

    public static UserRoleId From(string value) => new(value);
    public static UserRoleId New() => new(Ulid.NewUlid().ToString());
    public static implicit operator string(UserRoleId id) => id.Value;
}
