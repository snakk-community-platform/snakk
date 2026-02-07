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
