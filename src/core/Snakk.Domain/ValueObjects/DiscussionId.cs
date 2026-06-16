namespace Snakk.Domain.ValueObjects;

using System.Text.Json.Serialization;
using Snakk.Domain.Json;

[JsonConverter(typeof(DiscussionIdJsonConverter))]
public record DiscussionId
{
    public string Value { get; }

    private DiscussionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("DiscussionId cannot be empty", nameof(value));

        Value = value;
    }

    public static DiscussionId From(string value) => new(value);
    public static DiscussionId New() => new(Ulid.NewUlid().ToString());

    public override string ToString() => Value;

    public static implicit operator string(DiscussionId id) => id.Value;
}
