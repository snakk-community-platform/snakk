using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiscussionTypeEnum
{
    Standard = 0,
    Question = 1,
    Poll = 2,
    Announcement = 3,
    Link = 4,
    Images = 5,
    Guide = 6,
    Debate = 7,
    Journal = 8,
    Iama = 9
}
