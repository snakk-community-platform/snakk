namespace Snakk.Domain.ValueObjects;

public record UserAchievementId
{
    public string Value { get; }

    private UserAchievementId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("UserAchievementId cannot be empty", nameof(value));

        Value = value;
    }

    public static UserAchievementId From(string value) => new(value);
    public static UserAchievementId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(UserAchievementId id) => id.Value;
}
