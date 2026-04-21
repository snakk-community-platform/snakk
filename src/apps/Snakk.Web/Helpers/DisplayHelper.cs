namespace Snakk.Web.Helpers;

/// <summary>
/// Helper methods for formatting data for display in views.
/// </summary>
public static class DisplayHelper
{
    /// <summary>
    /// Formats a count into a compact, human-readable string.
    /// Examples: 1234 → "1.2k", 1500000 → "1.5m", 42 → "42"
    /// </summary>
    /// <param name="count">The count to format</param>
    /// <returns>A formatted string with suffix (k, m, b) for large numbers</returns>
    public static string FormatRelativeTime(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return "";
        var diff = DateTime.UtcNow - dateTime.Value;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 365) return dateTime.Value.ToString("MMM d");
        return dateTime.Value.ToString("MMM d, yyyy");
    }

    /// <summary>
    /// Formats the gap between two dates into a human-readable string like "2 months later".
    /// </summary>
    public static string FormatTimeBetween(DateTime a, DateTime b)
    {
        var days = (int)Math.Abs((b - a).TotalDays);

        if (days < 1) return "less than a day later";
        if (days == 1) return "1 day later";
        if (days < 7) return $"{days} days later";

        var weeks = days / 7;
        if (days < 30) return weeks == 1 ? "1 week later" : $"{weeks} weeks later";

        var months = days / 30;
        if (days < 365) return months == 1 ? "1 month later" : $"{months} months later";

        var years = days / 365;
        var remainingMonths = (days % 365) / 30;
        if (remainingMonths == 0) return years == 1 ? "1 year later" : $"{years} years later";
        return years == 1
            ? $"1 year, {remainingMonths} month{(remainingMonths > 1 ? "s" : "")} later"
            : $"{years} years, {remainingMonths} month{(remainingMonths > 1 ? "s" : "")} later";
    }

    public static string FormatFullDateTime(DateTime? dt) =>
        dt?.ToString("MMMM d, yyyy HH:mm") ?? "";

    public static string FormatCount(int count) =>
        count switch
        {
            >= 1_000_000_000 => 
                $"{count / 1_000_000_000.0:0.#}b",
            
            >= 10_000_000 => 
                $"{count / 1_000_000.0:0}m",
            
            >= 1_000_000 => 
                $"{count / 1_000_000.0:0.#}m",
            
            >= 10_000 => 
                $"{count / 1_000.0:0}k",
            
            >= 1_000 => 
                $"{count / 1_000.0:0.#}k",
            
            _ => count.ToString()
        };
}
