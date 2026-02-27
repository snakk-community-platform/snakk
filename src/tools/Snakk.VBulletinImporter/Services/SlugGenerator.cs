using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Snakk.VBulletinImporter.Services;

public static partial class SlugGenerator
{
    public static string Generate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "untitled";

        // Normalize unicode and strip diacritics
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var slug = sb.ToString().Normalize(NormalizationForm.FormC);

        // Norwegian characters that survive normalization
        slug = slug.Replace("æ", "ae").Replace("Æ", "ae")
                   .Replace("ø", "o").Replace("Ø", "o")
                   .Replace("å", "a").Replace("Å", "a");

        slug = slug.ToLowerInvariant();

        // Replace non-alphanumeric with hyphens
        slug = NonAlphanumericRegex().Replace(slug, "-");

        // Collapse multiple hyphens
        slug = MultiHyphenRegex().Replace(slug, "-");

        // Trim hyphens from ends
        slug = slug.Trim('-');

        if (string.IsNullOrEmpty(slug))
            return "untitled";

        // Cap length at 80 chars
        if (slug.Length > 80)
            slug = slug[..80].TrimEnd('-');

        return slug;
    }

    /// <summary>
    /// Generate a unique slug by appending a numeric suffix if needed.
    /// </summary>
    public static string GenerateUnique(string input, HashSet<string> existingSlugs)
    {
        var baseSlug = Generate(input);
        var slug = baseSlug;
        var counter = 2;

        while (existingSlugs.Contains(slug))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        existingSlugs.Add(slug);
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiHyphenRegex();
}
