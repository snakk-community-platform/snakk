using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReactionTypeEnum
{
    Agree = 1,      // 👍
    Love = 2,       // ❤️
    Funny = 3,      // 😂
    Thinking = 4,   // 🤔
    Watching = 5,   // 👀
    Fire = 6,       // 🔥
    Thanks = 7,     // 🙏
    MindBlown = 8,  // 🤯
    ShipIt = 9      // 🚀
}
