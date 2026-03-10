using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnnouncementScopeEnum
{
    Community = 0,
    Hub = 1,
    Space = 2
}
