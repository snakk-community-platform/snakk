using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReactionTypeEnum
{
    ThumbsUp = 1,   // 👍
    Heart = 2,      // ❤️
    Eyes = 3        // 👀
}
