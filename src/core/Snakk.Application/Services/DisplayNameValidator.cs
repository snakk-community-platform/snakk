namespace Snakk.Application.Services;

using System.Text.RegularExpressions;

public static partial class DisplayNameValidator
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Platform roles
        "admin", "administrator", "moderator", "mod",
        "owner", "staff", "team", "support",
        "system", "bot", "root", "webmaster",

        // Platform identity
        "snakk", "official", "verified",

        // Functional
        "help", "info", "contact",
        "banner", "announcements", "news",
        "security", "postmaster", "noreply",
        "abuse", "legal", "copyright", "dmca",

        // Common impersonation targets
        "anonymous", "anonymous user", "deleted", "deleted user",
        "unknown", "guest", "nobody", "everyone",
    };

    [GeneratedRegex(@"[\p{Cc}\p{Cf}\p{Zl}\p{Zp}]")]
    private static partial Regex InvisibleCharsRegex();

    [GeneratedRegex(@"^[\w\- ]+$")]
    private static partial Regex AllowedCharsRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpacesRegex();

    public static (bool IsValid, string? Error) Validate(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return (false, "Display name cannot be empty.");

        var trimmed = displayName.Trim();

        if (trimmed.Length < MinLength)
            return (false, $"Display name must be at least {MinLength} characters.");

        if (trimmed.Length > MaxLength)
            return (false, $"Display name must be at most {MaxLength} characters.");

        if (InvisibleCharsRegex().IsMatch(trimmed))
            return (false, "Display name contains invisible or control characters.");

        if (!AllowedCharsRegex().IsMatch(trimmed))
            return (false, "Display name can only contain letters, numbers, underscores, hyphens, and spaces.");

        if (MultipleSpacesRegex().IsMatch(trimmed))
            return (false, "Display name cannot contain consecutive spaces.");

        if (trimmed.StartsWith('-') || trimmed.EndsWith('-')
            || trimmed.StartsWith('_') || trimmed.EndsWith('_'))
            return (false, "Display name cannot start or end with a hyphen or underscore.");

        if (IsReservedName(trimmed))
            return (false, "This display name is reserved and cannot be used.");

        return (true, null);
    }

    public static bool IsReservedName(string name)
        => ReservedNames.Contains(name.Trim());
}
