using Snakk.Application.Services;

namespace Snakk.Application.Tests.Services;

public class ParagraphSplitterTests
{
    // Repeating "Sentence N. " to build triggerable walls of text.
    private static string MakeWall(int sentences)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= sentences; i++)
            sb.Append($"This is sentence number {i} in a long running paragraph of text. ");
        return sb.ToString().TrimEnd();
    }

    [Test]
    public async Task Split_EmptyString_ReturnsEmpty()
    {
        await Assert.That(ParagraphSplitter.Split("")).IsEqualTo("");
    }

    [Test]
    public async Task Split_ShortBlob_Unchanged()
    {
        var input = "This is a short post. It has only two sentences.";

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_FailsCharTrigger_Unchanged()
    {
        // Many sentences but under 500 chars.
        var input = "A. B. C. D. E. F. G. H.";

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_FailsSentenceTrigger_Unchanged()
    {
        // Over 500 chars but only one terminator.
        var input = new string('x', 600) + ".";

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_LongWallWithPunctuation_SplitsIntoMultipleParagraphs()
    {
        var input = MakeWall(15);

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).Contains("\n\n");
        var paragraphs = result.Split("\n\n");
        await Assert.That(paragraphs.Length).IsGreaterThan(1);
    }

    [Test]
    public async Task Split_LongWallNoPunctuation_Unchanged()
    {
        // >500 chars but no sentence terminators → trigger fails on sentence count.
        var input = string.Join(" ", Enumerable.Range(0, 150).Select(i => "word"));

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_ExplicitParagraphBreaks_EachBlockEvaluatedIndependently()
    {
        var shortBlock = "Short intro paragraph.";
        var longBlock = MakeWall(15);
        var input = shortBlock + "\n\n" + longBlock;

        var result = ParagraphSplitter.Split(input);

        // Short block stays intact, long block gets split.
        await Assert.That(result).StartsWith(shortBlock + "\n\n");
        var blocks = result.Split("\n\n");
        await Assert.That(blocks.Length).IsGreaterThan(2);
    }

    [Test]
    public async Task Split_FencedCodeBlock_Unchanged()
    {
        var code = "```\n" + MakeWall(15) + "\n```";

        var result = ParagraphSplitter.Split(code);

        await Assert.That(result).IsEqualTo(code);
    }

    [Test]
    public async Task Split_IndentedCodeBlock_Unchanged()
    {
        var lines = Enumerable.Range(0, 12)
            .Select(_ => "    This is an indented code line with words.");
        var code = string.Join("\n", lines);

        var result = ParagraphSplitter.Split(code);

        await Assert.That(result).IsEqualTo(code);
    }

    [Test]
    public async Task Split_BlockquoteBlock_Unchanged()
    {
        var input = "> " + MakeWall(15);

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_UnorderedListBlock_Unchanged()
    {
        var input = "- " + MakeWall(15);

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_OrderedListBlock_Unchanged()
    {
        var input = "1. " + MakeWall(15);

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_TableBlock_Unchanged()
    {
        var input = "| A | B |\n|---|---|\n| x | y |\n| z | w |";

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_AtxHeadingBlock_Unchanged()
    {
        var input = "# " + MakeWall(15);

        var result = ParagraphSplitter.Split(input);

        await Assert.That(result).IsEqualTo(input);
    }

    [Test]
    public async Task Split_Idempotent_RunningTwiceProducesSameResult()
    {
        var input = MakeWall(15);

        var first = ParagraphSplitter.Split(input);
        var second = ParagraphSplitter.Split(first);

        await Assert.That(second).IsEqualTo(first);
    }

    [Test]
    public async Task Split_PreservesAllWordContent()
    {
        var input = MakeWall(15);

        var result = ParagraphSplitter.Split(input);

        // Every non-whitespace character must still be present (no content loss).
        var before = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var after = new string(result.Where(c => !char.IsWhiteSpace(c)).ToArray());
        await Assert.That(after).IsEqualTo(before);
    }

    [Test]
    public async Task Split_UnbalancedInlineCode_DoesNotBreakMidCode()
    {
        // A long sentence that ends inside a code span — next sentence follows.
        // Candidate break after the first '.' would leave the backtick unbalanced,
        // so the splitter must not emit a break there.
        var a = "Here is a very long sentence that discusses some topic in quite a bit of detail and keeps going and going and going past the soft break threshold. ";
        var b = "Then the text continues with `inline code that spans. across. several. sentences. here. and. then` closes. ";
        var c = MakeWall(5);
        var input = a + b + c;

        var result = ParagraphSplitter.Split(input);

        // The code span must not be split: every paragraph has a balanced count
        // of backticks (even number).
        foreach (var p in result.Split("\n\n"))
        {
            var backticks = p.Count(ch => ch == '`');
            await Assert.That(backticks % 2).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Split_HardBreakFallback_ForcedAtLongRunWithoutPunctuation()
    {
        // One very long unpunctuated stretch, followed by a sentence-rich wall
        // so the trigger passes overall. The long run should still be broken
        // at whitespace to avoid runaway paragraphs.
        var bigRun = string.Join(" ", Enumerable.Range(0, 200).Select(_ => "word"));
        var input = bigRun + ". " + MakeWall(10);

        var result = ParagraphSplitter.Split(input);

        // No single paragraph should exceed roughly 2x the hard cap (allow slack
        // for whitespace alignment).
        foreach (var p in result.Split("\n\n"))
            await Assert.That(p.Length).IsLessThan(1600);
    }

    [Test]
    public async Task Split_CjkTerminator_CountedAsSentence()
    {
        // A wall using Chinese full stops — trigger must still fire.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 15; i++)
            sb.Append("这是一个很长的句子用来测试段落分割器是否能正确识别中文句号作为句子终止符号呀呀呀呀呀呀呀呀。");
        var input = sb.ToString();

        var result = ParagraphSplitter.Split(input);

        // Should produce multiple paragraphs.
        await Assert.That(result).Contains("\n\n");
    }
}
