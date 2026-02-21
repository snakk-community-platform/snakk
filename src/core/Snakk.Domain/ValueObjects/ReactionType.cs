namespace Snakk.Domain.ValueObjects;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReactionType
{
    ThumbsUp = 1,   // 👍
    Heart = 2,      // ❤️
    Eyes = 3,       // 👀
    Crazy = 4       // 🤯
}
