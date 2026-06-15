namespace Snakk.Shared.Helpers;

public static class ReservedNames
{
    private static readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "mod", "moderator", "moderators",
        "support", "help", "staff", "system", "snakk", "official",
        "root", "superuser", "webmaster", "security", "abuse",
        "noreply", "no-reply", "info", "contact", "team", "bot",
        "api", "bff", "auth", "login", "logout", "register", "signup",
        "settings", "account", "profile", "dashboard", "me", "my",
        "feed", "home", "search", "explore", "discover",
        "announcements", "news", "blog", "forum", "community",
        "communities", "spaces", "hub", "hubs", "trending", "top",
        "latest", "popular", "new", "notifications", "messages",
        "inbox", "reports", "banned", "suspended", "deleted",
        "undefined", "null", "true", "false",
    };

    public static bool IsReserved(string value) =>
        _blocked.Contains(value.Trim());
}
