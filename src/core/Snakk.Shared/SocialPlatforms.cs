namespace Snakk.Shared;

using System.Text.RegularExpressions;

public sealed record SocialPlatform(
    string Key,
    string DisplayName,
    string Category,
    string? UrlTemplate,      // {0} = stored username; null = display-only (Mastodon uses BuildUrl)
    string UsernamePattern,   // regex for stored-value validation
    string Placeholder,
    Func<string, string?> ParseInput);  // raw user input → normalised stored value; null = invalid

public static class SocialPlatformRegistry
{
    // ─── Private parse helpers ──────────────────────────────────────────────

    private static string? PathUsername(string input, params string[] domains)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input)) return null;
        var bare = input.TrimStart('@');

        if (Uri.TryCreate(input.Contains("://") ? input : "https://" + input, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            var host = uri.Host.ToLowerInvariant().TrimStart('w').TrimStart('w').TrimStart('w').TrimStart('.');
            // simpler: strip www. prefix
            host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host[4..];

            if (!domains.Any(d => host == d || host.EndsWith("." + d)))
                return string.IsNullOrEmpty(bare) ? null : bare; // treat as bare username if no domain match

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return null;
            return segments[^1].TrimStart('@');
        }

        return string.IsNullOrEmpty(bare) ? null : bare;
    }

    private static string? Subdomain(string input, string baseDomain)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input)) return null;

        if (Uri.TryCreate(input.Contains("://") ? input : "https://" + input, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            var host = uri.Host.ToLowerInvariant();
            if (host.EndsWith("." + baseDomain) && host != baseDomain)
                return host[..host.LastIndexOf("." + baseDomain)];
            return null;
        }

        var suffix = "." + baseDomain;
        if (input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return input[..^suffix.Length];

        return input.TrimStart('@');
    }

    private static string? ParseMastodon(string input)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input)) return null;
        if (input.StartsWith("@@")) return null;
        if (input.StartsWith('@')) input = input[1..];

        // user@instance.social
        if (Regex.IsMatch(input, @"^[\w.+\-]+@[\w.\-]+\.[a-zA-Z]{2,}$"))
            return input.ToLowerInvariant();

        // https://instance.social/@user
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            var segs = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length == 1 && segs[0].StartsWith('@'))
                return $"{segs[0][1..].ToLowerInvariant()}@{uri.Host.ToLowerInvariant()}";
        }

        return null;
    }

    private static string? ParseStackOverflow(string input)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input)) return null;

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var segs = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length >= 2 && segs[0] == "users" && long.TryParse(segs[1], out _))
                return segs[1];
            return null;
        }

        return long.TryParse(input, out _) ? input : null;
    }

    private static string? ParseDiscordId(string input)
    {
        var trimmed = input.Trim();
        // Extract from discord.com/users/{id} URL
        if (Uri.TryCreate(trimmed.Contains("://") ? trimmed : "https://" + trimmed, UriKind.Absolute, out var uri)
            && (uri.Host.Contains("discord.com") || uri.Host.Contains("discordapp.com")))
        {
            var seg = uri.AbsolutePath.Split('/').LastOrDefault(s => s.Length > 0);
            if (seg != null && Regex.IsMatch(seg, @"^\d{17,20}$"))
                return seg;
        }
        // Bare numeric snowflake ID
        return Regex.IsMatch(trimmed, @"^\d{17,20}$") ? trimmed : null;
    }

    // ─── Platform registry ──────────────────────────────────────────────────

    public static readonly IReadOnlyDictionary<string, SocialPlatform> All =
        new Dictionary<string, SocialPlatform>
        {
            // Social
            ["twitter"]      = new("twitter",      "Twitter / X",   "Social",    "https://x.com/{0}",                    @"^[A-Za-z0-9_]{1,50}$",             "username",                    i => PathUsername(i, "twitter.com", "x.com")),
            ["instagram"]    = new("instagram",    "Instagram",     "Social",    "https://instagram.com/{0}",            @"^[A-Za-z0-9_.]{1,30}$",            "username",                    i => PathUsername(i, "instagram.com")),
            ["threads"]      = new("threads",      "Threads",       "Social",    "https://www.threads.net/@{0}",         @"^[A-Za-z0-9_.]{1,30}$",            "username",                    i => PathUsername(i, "threads.net")),
            ["facebook"]     = new("facebook",     "Facebook",      "Social",    "https://facebook.com/{0}",             @"^[A-Za-z0-9.]{5,50}$",             "username or page name",       i => PathUsername(i, "facebook.com", "fb.com")),
            ["linkedin"]     = new("linkedin",     "LinkedIn",      "Social",    "https://linkedin.com/in/{0}",          @"^[A-Za-z0-9\-]{3,100}$",           "profile slug (from /in/…)",   i => PathUsername(i, "linkedin.com")),
            ["pinterest"]    = new("pinterest",    "Pinterest",     "Social",    "https://pinterest.com/{0}",            @"^[A-Za-z0-9_]{3,30}$",             "username",                    i => PathUsername(i, "pinterest.com")),
            ["reddit"]       = new("reddit",       "Reddit",        "Social",    "https://reddit.com/u/{0}",             @"^[A-Za-z0-9_\-]{3,20}$",           "username",                    i => PathUsername(i, "reddit.com", "old.reddit.com")),
            ["snapchat"]     = new("snapchat",     "Snapchat",      "Social",    "https://snapchat.com/add/{0}",         @"^[A-Za-z0-9_.\-]{3,15}$",          "username",                    i => PathUsername(i, "snapchat.com")),
            ["tiktok"]       = new("tiktok",       "TikTok",        "Social",    "https://tiktok.com/@{0}",              @"^[A-Za-z0-9_.]{1,24}$",            "username",                    i => PathUsername(i, "tiktok.com", "vm.tiktok.com")),

            // Fediverse
            ["bluesky"]      = new("bluesky",      "Bluesky",       "Fediverse", "https://bsky.app/profile/{0}",         @"^[A-Za-z0-9][A-Za-z0-9.\-]{0,100}$", "handle (e.g. user.bsky.social)", i => PathUsername(i, "bsky.app")),
            ["mastodon"]     = new("mastodon",     "Mastodon",      "Fediverse", null,                                   @"^[\w.+\-]+@[\w.\-]+\.[a-zA-Z]{2,}$",  "user@instance.social",       ParseMastodon),

            // Video
            ["youtube"]      = new("youtube",      "YouTube",       "Video",     "https://youtube.com/@{0}",             @"^@?[A-Za-z0-9_.\-]{3,50}$",        "@handle or channel",          i => PathUsername(i, "youtube.com", "youtu.be")),
            ["twitch"]       = new("twitch",       "Twitch",        "Video",     "https://twitch.tv/{0}",                @"^[A-Za-z0-9_]{4,25}$",             "username",                    i => PathUsername(i, "twitch.tv")),
            ["kick"]         = new("kick",         "Kick",          "Video",     "https://kick.com/{0}",                 @"^[A-Za-z0-9_\-]{3,30}$",           "username",                    i => PathUsername(i, "kick.com")),

            // Developer
            ["github"]       = new("github",       "GitHub",        "Developer", "https://github.com/{0}",               @"^[A-Za-z0-9](?:[A-Za-z0-9\-]{0,37}[A-Za-z0-9])?$", "username", i => PathUsername(i, "github.com")),
            ["gitlab"]       = new("gitlab",       "GitLab",        "Developer", "https://gitlab.com/{0}",               @"^[A-Za-z0-9_.\-]{1,255}$",         "username",                    i => PathUsername(i, "gitlab.com")),
            ["stackoverflow"]= new("stackoverflow","Stack Overflow", "Developer","https://stackoverflow.com/users/{0}",  @"^\d{1,10}$",                        "numeric user ID",             ParseStackOverflow),
            ["codepen"]      = new("codepen",      "CodePen",       "Developer", "https://codepen.io/{0}",               @"^[A-Za-z0-9_\-]{1,30}$",           "username",                    i => PathUsername(i, "codepen.io")),

            // Creative
            ["behance"]      = new("behance",      "Behance",       "Creative",  "https://behance.net/{0}",              @"^[A-Za-z0-9_.\-]{3,30}$",          "username",                    i => PathUsername(i, "behance.net")),
            ["dribbble"]     = new("dribbble",     "Dribbble",      "Creative",  "https://dribbble.com/{0}",             @"^[A-Za-z0-9_.\-]{2,30}$",          "username",                    i => PathUsername(i, "dribbble.com")),
            ["artstation"]   = new("artstation",   "ArtStation",    "Creative",  "https://artstation.com/{0}",           @"^[A-Za-z0-9_.\-]{3,40}$",          "username",                    i => PathUsername(i, "artstation.com")),
            ["deviantart"]   = new("deviantart",   "DeviantArt",    "Creative",  "https://deviantart.com/{0}",           @"^[A-Za-z0-9\-]{3,20}$",            "username",                    i => PathUsername(i, "deviantart.com")),
            ["soundcloud"]   = new("soundcloud",   "SoundCloud",    "Creative",  "https://soundcloud.com/{0}",           @"^[A-Za-z0-9_.\-]{3,25}$",          "username",                    i => PathUsername(i, "soundcloud.com")),
            ["bandcamp"]     = new("bandcamp",     "Bandcamp",      "Creative",  "https://{0}.bandcamp.com",             @"^[A-Za-z0-9\-]{1,40}$",            "your subdomain name",         i => Subdomain(i, "bandcamp.com")),
            ["letterboxd"]   = new("letterboxd",   "Letterboxd",    "Creative",  "https://letterboxd.com/{0}",           @"^[A-Za-z0-9_]{2,15}$",             "username",                    i => PathUsername(i, "letterboxd.com")),
            ["goodreads"]    = new("goodreads",    "Goodreads",     "Creative",  "https://goodreads.com/{0}",            @"^[A-Za-z0-9_.\-]{3,50}$",          "username",                    i => PathUsername(i, "goodreads.com")),

            // Gaming
            ["steam"]        = new("steam",        "Steam",         "Gaming",    "https://steamcommunity.com/id/{0}",   @"^[A-Za-z0-9_.\-]{2,32}$",          "custom URL ID",               i => PathUsername(i, "steamcommunity.com")),
            ["itch"]         = new("itch",         "Itch.io",       "Gaming",    "https://{0}.itch.io",                 @"^[A-Za-z0-9\-]{1,30}$",            "your subdomain name",         i => Subdomain(i, "itch.io")),

            // Creator
            ["kofi"]         = new("kofi",         "Ko-fi",         "Creator",   "https://ko-fi.com/{0}",               @"^[A-Za-z0-9_\-]{3,30}$",           "username",                    i => PathUsername(i, "ko-fi.com")),
            ["patreon"]      = new("patreon",      "Patreon",       "Creator",   "https://patreon.com/{0}",             @"^[A-Za-z0-9_\-]{1,50}$",           "username",                    i => PathUsername(i, "patreon.com")),

            // Messaging
            ["discord"]      = new("discord",      "Discord",       "Messaging", "https://discord.com/users/{0}",       @"^\d{17,20}$",                      "User ID (Developer Mode → right-click profile → Copy User ID)", ParseDiscordId),
            ["telegram"]     = new("telegram",     "Telegram",      "Messaging", "https://t.me/{0}",                    @"^[A-Za-z][A-Za-z0-9_]{4,31}$",     "username",                    i => PathUsername(i, "t.me", "telegram.me")),

        };

    public static bool IsValidKey(string key) => All.ContainsKey(key);

    // Builds the display URL from the stored username. Returns null if platform has no URL template.
    public static string? BuildUrl(SocialPlatform platform, string username)
    {
        if (platform.UrlTemplate is null)
        {
            // Mastodon: stored as user@instance.social → https://instance.social/@user
            if (platform.Key == "mastodon")
            {
                var at = username.IndexOf('@');
                if (at < 0) return null;
                return $"https://{username[(at + 1)..]}/@{username[..at]}";
            }
            return null;
        }

        return string.Format(platform.UrlTemplate, username);
    }
}
