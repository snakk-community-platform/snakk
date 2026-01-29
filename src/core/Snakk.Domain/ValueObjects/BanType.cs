namespace Snakk.Domain.ValueObjects;

public enum BanType
{
    WriteOnly,    // User can read but cannot post/reply
    ReadWrite     // User cannot read or write (full ban)
}
