namespace Snakk.Domain.ValueObjects;

public record CommunityId
{
    public string Value { get; }

    private CommunityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CommunityId cannot be empty", nameof(value));

        Value = value;
    }

    public static CommunityId From(string value) => new(value);
    public static CommunityId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(CommunityId id) => id.Value;
}
