namespace Snakk.Domain.ValueObjects;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReactionType
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
