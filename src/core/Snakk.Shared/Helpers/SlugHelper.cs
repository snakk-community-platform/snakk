using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Snakk.Shared.Helpers;

public static partial class SlugHelper
{
    /// <summary>
    /// Converts a title/name to a URL-safe slug.
    /// Strips diacritics, replaces spaces/underscores with hyphens,
    /// removes all non-alphanumeric characters (except hyphens),
    /// collapses consecutive hyphens, and trims leading/trailing hyphens.
    /// </summary>
    public static string ToSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Normalize unicode and strip diacritics (é → e, ü → u)
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC);

        // Lowercase
        result = result.ToLowerInvariant();

        // Replace common separators with hyphens
        result = result.Replace(' ', '-').Replace('_', '-');

        // Remove everything that isn't a letter, digit, or hyphen
        result = NonSlugChars().Replace(result, "");

        // Collapse consecutive hyphens
        result = ConsecutiveHyphens().Replace(result, "-");

        // Trim leading/trailing hyphens
        return result.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex("-{2,}")]
    private static partial Regex ConsecutiveHyphens();
}
