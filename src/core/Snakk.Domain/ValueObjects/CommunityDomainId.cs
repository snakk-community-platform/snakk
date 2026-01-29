namespace Snakk.Domain.ValueObjects;

public record CommunityDomainId
{
    public string Value { get; }

    private CommunityDomainId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CommunityDomainId cannot be empty", nameof(value));

        Value = value;
    }

    public static CommunityDomainId From(string value) => new(value);
    public static CommunityDomainId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(CommunityDomainId id) => id.Value;
}
