using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AdultPreviewImageModeEnum
{
    Show = 0,
    Blur = 1,
    Hide = 2
}
