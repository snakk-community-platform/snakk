namespace Snakk.Application.Services;

using System.Globalization;
using System.Text;

/// <summary>
/// Heuristic paragraph splitter for walls of text.
/// Language-agnostic: uses only structural signals (length, sentence terminators, whitespace).
/// Runs at write time, before markdown parsing. Idempotent.
/// </summary>
public static class ParagraphSplitter
{
    // Trigger thresholds — both must be true for a block to get split.
    private const int TriggerMinChars = 500;
    private const int TriggerMinSentences = 5;

    // Split behavior — start a new paragraph once the running buffer passes
    // SoftBreakChars at a sentence terminator, or HardBreakChars regardless.
    private const int SoftBreakChars = 400;
    private const int HardBreakChars = 800;

    // Short tails merge back into the previous paragraph so we don't orphan
    // a single trailing sentence.
    private const int MinTailChars = 80;

    // Latin-style terminators require trailing whitespace (or end-of-block) to
    // be treated as a sentence boundary. This avoids splitting on things like
    // "e.g.", "3.14", URLs, or abbreviations.
    private static readonly char[] LatinTerminators = ['.', '!', '?', '\u2026'];

    // Standalone terminators come from scripts that typically don't use spaces
    // between sentences (CJK, Arabic, Armenian, Devanagari). The terminator
    // itself is the boundary — no trailing whitespace required.
    private static readonly char[] StandaloneTerminators =
    [
        '\u3002', // 。
        '\uFF01', // ！
        '\uFF1F', // ？
        '\u061F', // ؟
        '\u0589', // ։
        '\u0964', // ।
    ];

    public static string Split(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Preserve user-authored paragraph breaks. Split on any run of 2+ newlines,
        // process each block independently, rejoin with \n\n.
        var blocks = SplitOnBlankLines(content);
        var output = new StringBuilder(content.Length + 64);

        for (int i = 0; i < blocks.Count; i++)
        {
            if (i > 0)
                output.Append("\n\n");

            var block = blocks[i];
            if (ShouldSkipBlock(block))
                output.Append(block);
            else
                output.Append(SplitBlock(block));
        }

        return output.ToString();
    }

    private static List<string> SplitOnBlankLines(string content)
    {
        // A "block" is a run of lines separated by a blank line (\n\n or \r\n\r\n).
        // We normalize CRLF → LF first so the split is simple.
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var rawBlocks = normalized.Split(["\n\n"], StringSplitOptions.None);

        // Trim surrounding blank lines off each block but keep internal content intact.
        var result = new List<string>(rawBlocks.Length);
        foreach (var rb in rawBlocks)
        {
            var trimmed = rb.Trim('\n');
            result.Add(trimmed);
        }
        return result;
    }

    private static bool ShouldSkipBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
            return true;

        var firstLine = GetFirstLine(block);

        // Fenced code block — starts with ``` or ~~~
        if (firstLine.StartsWith("```", StringComparison.Ordinal) || firstLine.StartsWith("~~~", StringComparison.Ordinal))
            return true;

        // Indented code block — every non-empty line starts with 4+ spaces or a tab
        if (IsIndentedCode(block))
            return true;

        // List — first line starts with "- ", "* ", "+ " or "N. " / "N) "
        if (IsListStart(firstLine))
            return true;

        // Blockquote — first line starts with ">"
        if (firstLine.StartsWith('>'))
            return true;

        // Table — any line contains a pipe AND is not a simple inline mention
        if (IsTable(block))
            return true;

        // Setext heading — line 2 is === or ---
        if (IsSetextHeading(block))
            return true;

        // ATX heading — first line starts with 1-6 '#' followed by space
        if (IsAtxHeading(firstLine))
            return true;

        return false;
    }

    private static string GetFirstLine(string block)
    {
        int nl = block.IndexOf('\n');
        return nl < 0 ? block : block[..nl];
    }

    private static bool IsIndentedCode(string block)
    {
        foreach (var line in block.Split('\n'))
        {
            if (line.Length == 0)
                continue;
            if (!(line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith('\t')))
                return false;
        }
        return true;
    }

    private static bool IsListStart(string firstLine)
    {
        var trimmed = firstLine.TrimStart();
        if (trimmed.Length < 2)
            return false;

        // Unordered
        if ((trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+') && trimmed[1] == ' ')
            return true;

        // Ordered: digits followed by . or ) and a space
        int i = 0;
        while (i < trimmed.Length && char.IsDigit(trimmed[i]))
            i++;
        if (i > 0 && i < trimmed.Length - 1 && (trimmed[i] == '.' || trimmed[i] == ')') && trimmed[i + 1] == ' ')
            return true;

        return false;
    }

    private static bool IsTable(string block)
    {
        int pipeLines = 0;
        int totalLines = 0;
        foreach (var line in block.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            totalLines++;
            if (line.Contains('|'))
                pipeLines++;
        }
        // Markdown tables need at least a header row and a separator row.
        // Heuristic: a block is a table if most lines contain a pipe.
        return totalLines >= 2 && pipeLines >= 2 && pipeLines * 2 >= totalLines;
    }

    private static bool IsSetextHeading(string block)
    {
        int nl = block.IndexOf('\n');
        if (nl < 0 || nl == block.Length - 1)
            return false;
        var secondLine = block[(nl + 1)..];
        int nl2 = secondLine.IndexOf('\n');
        if (nl2 >= 0)
            secondLine = secondLine[..nl2];
        secondLine = secondLine.TrimEnd();
        if (secondLine.Length < 2)
            return false;
        char c = secondLine[0];
        if (c != '=' && c != '-')
            return false;
        foreach (var ch in secondLine)
            if (ch != c)
                return false;
        return true;
    }

    private static bool IsAtxHeading(string firstLine)
    {
        var trimmed = firstLine.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '#')
            return false;
        int i = 0;
        while (i < trimmed.Length && trimmed[i] == '#' && i < 6)
            i++;
        return i >= 1 && i <= 6 && i < trimmed.Length && trimmed[i] == ' ';
    }

    private static string SplitBlock(string block)
    {
        // Count sentences and chars; bail early if trigger fails.
        int charCount = block.Length;
        int sentenceCount = CountSentences(block);

        if (charCount <= TriggerMinChars || sentenceCount <= TriggerMinSentences)
            return block;

        // Walk forward, finding break candidates at sentence terminators.
        // Fall back to whitespace-break when past HardBreakChars.
        var paragraphs = new List<string>();
        int segStart = 0;
        int lastBreakable = -1; // last sentence-terminator position we could have broken on

        for (int i = 0; i < block.Length; i++)
        {
            char c = block[i];

            if (IsAnyTerminator(c) && IsInlineSafe(block, segStart, i + 1))
            {
                // Collapse runs of terminators (e.g. "!!" "?!").
                int runEnd = i + 1;
                while (runEnd < block.Length && IsAnyTerminator(block[runEnd]))
                    runEnd++;

                // A terminator run is a sentence boundary if:
                //   - it's at end-of-block, or
                //   - followed by whitespace (Latin convention), or
                //   - any terminator in the run is standalone (CJK convention)
                bool boundary = runEnd == block.Length
                    || char.IsWhiteSpace(block[runEnd])
                    || HasStandaloneInRange(block, i, runEnd);

                if (boundary)
                {
                    lastBreakable = runEnd;
                    int segLen = runEnd - segStart;
                    if (segLen >= SoftBreakChars)
                    {
                        paragraphs.Add(block[segStart..runEnd].TrimEnd());
                        segStart = SkipWhitespace(block, runEnd);
                        lastBreakable = -1;
                        i = segStart - 1;
                        continue;
                    }
                }
                i = runEnd - 1;
                continue;
            }

            // Hard-break fallback: no punctuation found for a long stretch.
            if (i - segStart >= HardBreakChars)
            {
                int breakAt = FindNearestWhitespace(block, i);
                if (breakAt > segStart && IsInlineSafe(block, segStart, breakAt))
                {
                    paragraphs.Add(block[segStart..breakAt].TrimEnd());
                    segStart = SkipWhitespace(block, breakAt);
                    lastBreakable = -1;
                    i = segStart - 1;
                    continue;
                }
            }
        }

        // Tail.
        var tail = block[segStart..].TrimEnd();
        if (tail.Length > 0)
        {
            if (paragraphs.Count > 0 && tail.Length < MinTailChars)
                paragraphs[^1] = paragraphs[^1] + " " + tail;
            else
                paragraphs.Add(tail);
        }

        if (paragraphs.Count <= 1)
            return block;

        return string.Join("\n\n", paragraphs);
    }

    private static int CountSentences(string block)
    {
        int count = 0;
        int i = 0;
        while (i < block.Length)
        {
            if (!IsAnyTerminator(block[i]))
            {
                i++;
                continue;
            }

            int runStart = i;
            int runEnd = i + 1;
            while (runEnd < block.Length && IsAnyTerminator(block[runEnd]))
                runEnd++;

            bool boundary = runEnd == block.Length
                || char.IsWhiteSpace(block[runEnd])
                || HasStandaloneInRange(block, runStart, runEnd);

            if (boundary)
                count++;

            i = runEnd;
        }
        return count;
    }

    private static bool IsAnyTerminator(char c) =>
        IsLatinTerminator(c) || IsStandaloneTerminator(c);

    private static bool IsLatinTerminator(char c)
    {
        foreach (var t in LatinTerminators)
            if (c == t)
                return true;
        return false;
    }

    private static bool IsStandaloneTerminator(char c)
    {
        foreach (var t in StandaloneTerminators)
            if (c == t)
                return true;
        return false;
    }

    private static bool HasStandaloneInRange(string block, int start, int end)
    {
        for (int i = start; i < end; i++)
            if (IsStandaloneTerminator(block[i]))
                return true;
        return false;
    }

    private static int SkipWhitespace(string s, int index)
    {
        while (index < s.Length && char.IsWhiteSpace(s[index]))
            index++;
        return index;
    }

    private static int FindNearestWhitespace(string s, int index)
    {
        // Prefer the nearest whitespace at or before index; if none, look forward.
        for (int i = index; i >= 0; i--)
            if (char.IsWhiteSpace(s[i]))
                return i;
        for (int i = index + 1; i < s.Length; i++)
            if (char.IsWhiteSpace(s[i]))
                return i;
        return -1;
    }

    /// <summary>
    /// Checks that the range [start, end) doesn't leave an inline construct open:
    /// unbalanced backticks (inline code), an open link '[' without its ')',
    /// or unbalanced spoiler ||...||. If any are unbalanced, the break would
    /// land inside a markdown inline and corrupt it.
    /// </summary>
    private static bool IsInlineSafe(string block, int start, int end)
    {
        int backticks = 0;
        int openLinkBracket = 0;
        int openLinkParen = 0;
        bool insideLinkUrl = false;
        int spoilerPipes = 0;

        for (int i = start; i < end; i++)
        {
            char c = block[i];

            if (c == '`')
            {
                backticks++;
            }
            else if (c == '[')
            {
                openLinkBracket++;
            }
            else if (c == ']' && openLinkBracket > 0)
            {
                openLinkBracket--;
                if (i + 1 < block.Length && block[i + 1] == '(')
                {
                    insideLinkUrl = true;
                    openLinkParen++;
                    i++;
                }
            }
            else if (c == '(' && insideLinkUrl)
            {
                openLinkParen++;
            }
            else if (c == ')' && insideLinkUrl)
            {
                openLinkParen--;
                if (openLinkParen == 0)
                    insideLinkUrl = false;
            }
            else if (c == '|' && i + 1 < block.Length && block[i + 1] == '|')
            {
                spoilerPipes++;
                i++;
            }
        }

        if (backticks % 2 != 0)
            return false;
        if (openLinkBracket > 0 || insideLinkUrl)
            return false;
        if (spoilerPipes % 2 != 0)
            return false;
        return true;
    }
}
