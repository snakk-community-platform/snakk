using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BannerScopeEnum
{
    Community = 0,
    Hub = 1,
    Space = 2
}
