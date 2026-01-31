using System.Text.Json.Serialization;

namespace Snakk.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BanTypeEnum
{
    WriteOnly = 1,    // User can read but cannot post/reply
    ReadWrite = 2     // User cannot read or write (full ban)
}
