namespace Snakk.Shared.Models;

using System.Text;

/// <summary>
/// Helpers for keyset pagination cursors.
/// Cursor format: base64("{sortValue}~{id}") where sortValue is a DateTime tick count.
/// </summary>
public static class Cursor
{
    /// <summary>
    /// Create a cursor from a DateTime and an integer ID.
    /// </summary>
    public static string Encode(DateTime? dateTime, int id)
    {
        var ticks = dateTime?.Ticks ?? 0;
        var raw = $"{ticks}~{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Decode a cursor to DateTime and integer ID.
    /// </summary>
    public static (DateTime? DateTime, int Id)? Decode(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return null;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('~');
            if (parts.Length != 2)
                return null;

            var ticks = long.Parse(parts[0]);
            var id = int.Parse(parts[1]);
            var dateTime = ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;
            return (dateTime, id);
        }
        catch
        {
            return null;
        }
    }
}
