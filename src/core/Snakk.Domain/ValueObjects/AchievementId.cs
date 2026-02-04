namespace Snakk.Domain.ValueObjects;

public record AchievementId
{
    public string Value { get; }

    private AchievementId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AchievementId cannot be empty", nameof(value));

        Value = value;
    }

    public static AchievementId From(string value) => new(value);
    public static AchievementId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(AchievementId id) => id.Value;
}
