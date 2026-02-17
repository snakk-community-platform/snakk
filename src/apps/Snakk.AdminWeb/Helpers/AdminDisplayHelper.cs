namespace Snakk.AdminWeb.Helpers;

public static class AdminDisplayHelper
{
    public static string FormatTimeAgo(DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime;

        if (timeSpan.TotalSeconds < 60)
            return "just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";
        if (timeSpan.TotalDays < 30)
            return $"{(int)(timeSpan.TotalDays / 7)}w ago";
        if (timeSpan.TotalDays < 365)
            return $"{(int)(timeSpan.TotalDays / 30)}mo ago";

        return $"{(int)(timeSpan.TotalDays / 365)}y ago";
    }

    public static string FormatNumber(int number)
    {
        if (number >= 1000000)
            return $"{number / 1000000.0:0.#}M";
        if (number >= 1000)
            return $"{number / 1000.0:0.#}K";

        return number.ToString();
    }

    public static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    public static string GetBadgeClass(string status)
    {
        return status.ToLower() switch
        {
            "pending" => "badge-warning",
            "resolved" => "badge-success",
            "dismissed" => "badge-secondary",
            "active" => "badge-success",
            "banned" => "badge-danger",
            _ => "badge-primary"
        };
    }
}
