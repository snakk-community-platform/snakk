using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BannerTypeEnum
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
