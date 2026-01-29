namespace Snakk.Domain.ValueObjects;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReactionType
{
    ThumbsUp,   // 👍
    Heart,      // ❤️
    Eyes        // 👀
}
