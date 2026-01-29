namespace Snakk.Domain.ValueObjects;

public record UserBanId
{
    public string Value { get; }

    private UserBanId(string value)
    {
        Value = value;
    }

    public static UserBanId From(string value) => new(value);
    public static UserBanId New() => new(Ulid.NewUlid().ToString());
    public static implicit operator string(UserBanId id) => id.Value;
}
