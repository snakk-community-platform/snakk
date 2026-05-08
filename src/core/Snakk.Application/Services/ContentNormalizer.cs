namespace Snakk.Application.Services;

using System.Text;
using System.Text.RegularExpressions;

public interface IContentNormalizer
{
    NormalizationResult NormalizeBody(string content);
    NormalizationResult NormalizeTitle(string title);
}

public record NormalizationResult(string Content, bool WasNormalized);

public class ContentNormalizer : IContentNormalizer
{
    private const double AllCapRatio = 0.75;
    private const int AllCapMinWords = 4;
    private const int AllCapMinAlpha = 5;

    // 5+ same word-char in a row → 3 (e.g. "sooooo" → "sooo")
    private static readonly Regex LaxRepetition = new(@"(\w)\1{4,}", RegexOptions.Compiled);

    public NormalizationResult NormalizeBody(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new(content, false);

        var lines = content.Split('\n');
        var resultLines = new string[lines.Length];
        bool wasNormalized = false;
        bool insideFencedBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                insideFencedBlock = !insideFencedBlock;
                resultLines[i] = line;
                continue;
            }

            if (insideFencedBlock || trimmed.StartsWith(">", StringComparison.Ordinal) || trimmed.StartsWith("|", StringComparison.Ordinal))
            {
                resultLines[i] = line;
                continue;
            }

            var (normalized, changed) = NormalizeSegment(line);
            resultLines[i] = normalized;
            if (changed) wasNormalized = true;
        }

        return new(string.Join('\n', resultLines), wasNormalized);
    }

    public NormalizationResult NormalizeTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return new(title, false);

        var (normalized, changed) = NormalizeSegment(title);
        return new(normalized, changed);
    }

    private static (string Text, bool Changed) NormalizeSegment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (text, false);

        var result = text;

        if (IsAllCaps(result))
            result = ToSentenceCase(result);

        result = NormalizeExcessivePunctuation(result);
        result = LaxRepetition.Replace(result, "$1$1$1");

        return (result, result != text);
    }

    private static bool IsAllCaps(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < AllCapMinWords)
            return false;

        int upper = 0, total = 0;
        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                total++;
                if (char.IsUpper(c)) upper++;
            }
        }

        return total >= AllCapMinAlpha && (double)upper / total >= AllCapRatio;
    }

    private static string ToSentenceCase(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool nextUp = true;
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (char.IsLetter(c))
            {
                int wordStart = i;
                while (i < text.Length && char.IsLetterOrDigit(text[i]))
                    i++;

                var word = text[wordStart..i];
                int alphaCount = word.Count(ch => char.IsLetter(ch));

                // Preserve short all-caps tokens (acronyms like OK, EU, NASA)
                bool isShortAllCaps = alphaCount is > 0 and <= 4
                    && word.All(ch => !char.IsLetter(ch) || char.IsUpper(ch));

                if (isShortAllCaps)
                {
                    sb.Append(word);
                    nextUp = false;
                }
                else if (nextUp)
                {
                    sb.Append(char.ToUpperInvariant(word[0]));
                    if (word.Length > 1) sb.Append(word[1..].ToLowerInvariant());
                    nextUp = false;
                }
                else
                {
                    sb.Append(word.ToLowerInvariant());
                }
            }
            else
            {
                if (c is '.' or '!' or '?')
                {
                    int next = i + 1;
                    if (next >= text.Length || char.IsWhiteSpace(text[next]))
                        nextUp = true;
                }
                else if (c == '\n')
                {
                    nextUp = true;
                }

                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    private static string NormalizeExcessivePunctuation(string text)
    {
        // 4+ dots → ellipsis (preserves "...")
        text = Regex.Replace(text, @"\.{4,}", "...");
        // 2+ of ! and/or ? → ! if any !, else ?
        text = Regex.Replace(text, @"[!?]{2,}", m => m.Value.Contains('!') ? "!" : "?");
        return text;
    }
}
