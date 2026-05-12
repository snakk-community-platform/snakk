namespace Snakk.Application.Services;

using Snakk.Shared.Enums;
using System.Text.RegularExpressions;

public sealed partial class DisplayNameValidator(ISettingsService settings)
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
        "anonymous", "deleted", "unknown", "guest", "nobody", "everyone",
    };

    [GeneratedRegex(@"[\p{Cc}\p{Cf}\p{Zl}\p{Zp}]")]
    private static partial Regex InvisibleCharsRegex();

    public async Task<(bool IsValid, string? Error)> ValidateAsync(string displayName)
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

        var allowedScripts = await settings.GetAllowedDisplayNameScriptsAsync();
        var allowedCharsRegex = ScriptGroupRegistry.BuildAllowedCharsRegex(allowedScripts);

        if (!allowedCharsRegex.IsMatch(trimmed))
            return (false, "Display name contains characters that are not allowed on this platform.");

        // All letter characters must come from a single script family.
        // Digits, hyphens, and underscores are neutral and do not count.
        var letterScriptFamilies = trimmed
            .Select(ScriptGroupRegistry.GetCharScriptFamily)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .Distinct()
            .ToList();

        if (letterScriptFamilies.Count > 1)
            return (false, "Display name must use characters from a single writing system.");

        if (trimmed.StartsWith('-') || trimmed.EndsWith('-')
            || trimmed.StartsWith('_') || trimmed.EndsWith('_'))
            return (false, "Display name cannot start or end with a hyphen or underscore.");

        if (IsReservedName(trimmed))
            return (false, "This display name is reserved and cannot be used.");

        return (true, null);
    }

    public static bool IsReservedName(string name) => ReservedNames.Contains(name.Trim());
}
