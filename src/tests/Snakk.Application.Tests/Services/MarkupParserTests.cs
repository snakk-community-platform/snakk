using Snakk.Application.Services;

namespace Snakk.Application.Tests.Services;

public class MarkupParserTests
{
    private readonly MarkupParser _parser = new();

    #region XSS Prevention Tests (CRITICAL SECURITY)

    [Test]
    public async Task ToHtml_WithScriptTag_EscapesScriptTag()
    {
        var result = _parser.ToHtml("<script>alert('XSS')</script>");

        await Assert.That(result).DoesNotContain("<script>");
        await Assert.That(result).DoesNotContain("</script>");
        await Assert.That(result).Contains("&lt;script&gt;");
        await Assert.That(result).Contains("&lt;/script&gt;");
    }

    [Test]
    public async Task ToHtml_WithOnClickAttribute_EscapesAttribute()
    {
        var result = _parser.ToHtml("<img src=x onerror=alert('XSS')>");

        // The entire tag should be escaped, not rendered as HTML
        await Assert.That(result).DoesNotContain("<img ");
        await Assert.That(result).Contains("&lt;img");
    }

    [Test]
    public async Task ToHtml_WithIframe_EscapesIframe()
    {
        var result = _parser.ToHtml("<iframe src='javascript:alert(1)'></iframe>");

        await Assert.That(result).DoesNotContain("<iframe");
        await Assert.That(result).Contains("&lt;iframe");
    }

    [Test]
    public async Task ToHtml_WithStyleTag_EscapesStyleTag()
    {
        var result = _parser.ToHtml("<style>body { background: url('javascript:alert(1)') }</style>");

        await Assert.That(result).DoesNotContain("<style>");
        await Assert.That(result).Contains("&lt;style&gt;");
    }

    [Test]
    public async Task ToHtml_WithObjectTag_EscapesObjectTag()
    {
        var result = _parser.ToHtml("<object data='javascript:alert(1)'></object>");

        await Assert.That(result).DoesNotContain("<object");
        await Assert.That(result).Contains("&lt;object");
    }

    [Test]
    public async Task ToHtml_WithEmbedTag_EscapesEmbedTag()
    {
        var result = _parser.ToHtml("<embed src='javascript:alert(1)'>");

        await Assert.That(result).DoesNotContain("<embed");
        await Assert.That(result).Contains("&lt;embed");
    }

    [Test]
    [Arguments("<div>test</div>")]
    [Arguments("<span onclick='alert(1)'>test</span>")]
    [Arguments("<svg onload=alert(1)>")]
    public async Task ToHtml_WithAnyHtmlTag_EscapesTag(string maliciousInput)
    {
        var result = _parser.ToHtml(maliciousInput);

        await Assert.That(result).Contains("&lt;");
        await Assert.That(result).Contains("&gt;");
    }

    #endregion

    #region URL Validation Tests (CRITICAL SECURITY)

    [Test]
    public async Task ToHtml_WithJavaScriptProtocol_DoesNotCreateLink()
    {
        var result = _parser.ToHtml("[click me](javascript:alert('XSS'))");

        await Assert.That(result).DoesNotContain("href=\"javascript:");
    }

    [Test]
    public async Task ToHtml_WithDataProtocol_DoesNotCreateLink()
    {
        var result = _parser.ToHtml("[click me](data:text/html,<script>alert(1)</script>)");

        await Assert.That(result).DoesNotContain("href=\"data:");
    }

    [Test]
    public async Task ToHtml_WithVbscriptProtocol_DoesNotCreateLink()
    {
        var result = _parser.ToHtml("[click me](vbscript:msgbox(1))");

        await Assert.That(result).DoesNotContain("href=\"vbscript:");
    }

    [Test]
    public async Task ToHtml_WithFileProtocol_DoesNotCreateLink()
    {
        var result = _parser.ToHtml("[click me](file:///etc/passwd)");

        await Assert.That(result).DoesNotContain("href=\"file:");
    }

    [Test]
    public async Task ToHtml_WithHttpUrl_CreatesLink()
    {
        var result = _parser.ToHtml("[Google](http://google.com)");

        await Assert.That(result).Contains("<a href=\"http://google.com\"");
        await Assert.That(result).Contains("target=\"_blank\"");
        await Assert.That(result).Contains("rel=\"nofollow noopener noreferrer\"");
        await Assert.That(result).Contains(">Google</a>");
    }

    [Test]
    public async Task ToHtml_WithHttpsUrl_CreatesLink()
    {
        var result = _parser.ToHtml("[Secure Site](https://example.com)");

        await Assert.That(result).Contains("<a href=\"https://example.com\"");
        await Assert.That(result).Contains(">Secure Site</a>");
    }

    [Test]
    public async Task ToHtml_WithMailtoUrl_CreatesLink()
    {
        var result = _parser.ToHtml("[Email me](mailto:test@example.com)");

        await Assert.That(result).Contains("<a href=\"mailto:test@example.com\"");
        await Assert.That(result).Contains(">Email me</a>");
    }

    [Test]
    public async Task ToHtml_WithRelativeUrl_CreatesLink()
    {
        var result = _parser.ToHtml("[Internal Link](/some/path)");

        await Assert.That(result).Contains("<a href=\"/some/path\"");
        await Assert.That(result).Contains(">Internal Link</a>");
    }

    [Test]
    public async Task ToHtml_WithProtocolRelativeUrl_DoesNotCreateLink()
    {
        var result = _parser.ToHtml("[Link](//example.com/path)");

        await Assert.That(result).DoesNotContain("href=\"//example.com");
    }

    #endregion

    #region Bold Formatting Tests

    [Test]
    public async Task ToHtml_WithAsteriskBold_ConvertsToBold()
    {
        var result = _parser.ToHtml("This is **bold** text");

        await Assert.That(result).Contains("<strong>bold</strong>");
    }

    [Test]
    public async Task ToHtml_WithUnderscoreBold_ConvertsToBold()
    {
        var result = _parser.ToHtml("This is __bold__ text");

        await Assert.That(result).Contains("<strong>bold</strong>");
    }

    [Test]
    public async Task ToHtml_WithMultipleBoldSections_ConvertsAll()
    {
        var result = _parser.ToHtml("**first** and **second** bold");

        await Assert.That(result).Contains("<strong>first</strong>");
        await Assert.That(result).Contains("<strong>second</strong>");
    }

    #endregion

    #region Italic Formatting Tests

    [Test]
    public async Task ToHtml_WithAsteriskItalic_ConvertsToItalic()
    {
        var result = _parser.ToHtml("This is *italic* text");

        await Assert.That(result).Contains("<em>italic</em>");
    }

    [Test]
    public async Task ToHtml_WithUnderscoreItalic_ConvertsToItalic()
    {
        var result = _parser.ToHtml("This is _italic_ text");

        await Assert.That(result).Contains("<em>italic</em>");
    }

    #endregion

    #region Combined Bold and Italic Tests

    [Test]
    public async Task ToHtml_WithBoldAndItalic_ConvertsBoth()
    {
        var result = _parser.ToHtml("**bold** and *italic*");

        await Assert.That(result).Contains("<strong>bold</strong>");
        await Assert.That(result).Contains("<em>italic</em>");
    }

    [Test]
    public async Task ToHtml_WithNestedBoldItalic_HandlesCorrectly()
    {
        var result = _parser.ToHtml("***bold and italic***");

        await Assert.That(result).Contains("<strong>");
        await Assert.That(result).Contains("<em>");
    }

    #endregion

    #region Inline Code Tests

    [Test]
    public async Task ToHtml_WithInlineCode_ConvertsToCode()
    {
        var result = _parser.ToHtml("Use the `code` function");

        await Assert.That(result).Contains("<code>code</code>");
    }

    [Test]
    public async Task ToHtml_WithMultipleInlineCodes_ConvertsAll()
    {
        var result = _parser.ToHtml("Use `func1()` or `func2()`");

        await Assert.That(result).Contains("<code>func1()</code>");
        await Assert.That(result).Contains("<code>func2()</code>");
    }

    [Test]
    public async Task ToHtml_WithHtmlInInlineCode_EscapesHtml()
    {
        var result = _parser.ToHtml("Code: `<script>alert(1)</script>`");

        await Assert.That(result).Contains("&lt;script&gt;");
        await Assert.That(result).DoesNotContain("<script>");
    }

    #endregion

    #region Code Block Tests

    [Test]
    public async Task ToHtml_WithCodeBlock_ConvertsToPreCode()
    {
        var result = _parser.ToHtml("```\nfunction test() {\n  return 42;\n}\n```");

        await Assert.That(result).Contains("<pre><code>");
    }

    [Test]
    public async Task ToHtml_WithHtmlInCodeBlock_EscapesHtml()
    {
        var result = _parser.ToHtml("```\n<script>alert('XSS')</script>\n```");

        await Assert.That(result).Contains("&lt;script&gt;");
        await Assert.That(result).DoesNotContain("<script>");
    }

    [Test]
    public async Task ToHtml_WithCodeBlock_DoesNotApplyFormatting()
    {
        var result = _parser.ToHtml("```\n**not bold** and *not italic*\n```");

        await Assert.That(result).DoesNotContain("<strong>");
        await Assert.That(result).DoesNotContain("<em>");
    }

    [Test]
    public async Task ToHtml_WithLanguageHint_AddsLanguageClass()
    {
        var result = _parser.ToHtml("```csharp\nvar x = 42;\n```");

        await Assert.That(result).Contains("class=\"language-csharp\"");
        await Assert.That(result).Contains("var x = 42;");
    }

    [Test]
    public async Task ToHtml_WithCodeBlockNoLanguage_NoLanguageClass()
    {
        var result = _parser.ToHtml("```\nsome code\n```");

        await Assert.That(result).Contains("<code>");
        await Assert.That(result).DoesNotContain("class=\"language-");
    }

    [Test]
    public async Task ToHtml_WithLanguageHint_NormalizesToLowercase()
    {
        var result = _parser.ToHtml("```JavaScript\nconsole.log('hi');\n```");

        await Assert.That(result).Contains("class=\"language-javascript\"");
    }

    #endregion

    #region Heading Tests

    [Test]
    public async Task ToHtml_WithHeading1_ConvertsToH1()
    {
        var result = _parser.ToHtml("# Heading 1");

        await Assert.That(result).Contains("<h1");
        await Assert.That(result).Contains("Heading 1");
        await Assert.That(result).Contains("</h1>");
    }

    [Test]
    public async Task ToHtml_WithHeading2_ConvertsToH2()
    {
        var result = _parser.ToHtml("## Heading 2");

        await Assert.That(result).Contains("<h2");
        await Assert.That(result).Contains("Heading 2");
    }

    [Test]
    public async Task ToHtml_WithHeading3_ConvertsToH3()
    {
        var result = _parser.ToHtml("### Heading 3");

        await Assert.That(result).Contains("<h3");
        await Assert.That(result).Contains("Heading 3");
    }

    #endregion

    #region Strikethrough Tests

    [Test]
    public async Task ToHtml_WithStrikethrough_ConvertsToDelTag()
    {
        var result = _parser.ToHtml("~~deleted text~~");

        await Assert.That(result).Contains("<del>deleted text</del>");
    }

    #endregion

    #region Horizontal Rule Tests

    [Test]
    public async Task ToHtml_WithHorizontalRule_ConvertsToHr()
    {
        var result = _parser.ToHtml("text above\n\n---\n\ntext below");

        await Assert.That(result).Contains("<hr");
    }

    #endregion

    #region ContainsCode Tests

    [Test]
    public async Task ContainsCode_WithInlineCode_ReturnsTrue()
    {
        await Assert.That(MarkupParser.ContainsCode("Use `code` here")).IsTrue();
    }

    [Test]
    public async Task ContainsCode_WithCodeBlock_ReturnsTrue()
    {
        await Assert.That(MarkupParser.ContainsCode("```\ncode\n```")).IsTrue();
    }

    [Test]
    public async Task ContainsCode_WithPlainText_ReturnsFalse()
    {
        await Assert.That(MarkupParser.ContainsCode("Just plain text")).IsFalse();
    }

    [Test]
    public async Task ContainsCode_WithEmpty_ReturnsFalse()
    {
        await Assert.That(MarkupParser.ContainsCode("")).IsFalse();
    }

    #endregion

    #region Blockquote Tests

    [Test]
    public async Task ToHtml_WithBlockquote_ConvertsToBlockquote()
    {
        var result = _parser.ToHtml("> This is a quote");

        await Assert.That(result).Contains("<blockquote");
        await Assert.That(result).Contains("This is a quote");
        await Assert.That(result).Contains("</blockquote>");
    }

    [Test]
    public async Task ToHtml_WithMultilineBlockquote_KeepsInSameBlockquote()
    {
        var result = _parser.ToHtml("> Line 1\n> Line 2\n> Line 3");

        await Assert.That(result).Contains("Line 1");
        await Assert.That(result).Contains("Line 2");
        await Assert.That(result).Contains("Line 3");
        // Should only have one blockquote
        await Assert.That(result.Split("<blockquote")).Count().IsEqualTo(2);
        await Assert.That(result.Split("</blockquote>")).Count().IsEqualTo(2);
    }

    #endregion

    #region List Tests

    [Test]
    public async Task ToHtml_WithUnorderedList_ConvertsToUl()
    {
        var result = _parser.ToHtml("- Item 1\n- Item 2\n- Item 3");

        await Assert.That(result).Contains("<ul");
        await Assert.That(result).Contains("<li>Item 1</li>");
        await Assert.That(result).Contains("<li>Item 2</li>");
        await Assert.That(result).Contains("<li>Item 3</li>");
        await Assert.That(result).Contains("</ul>");
    }

    [Test]
    public async Task ToHtml_WithOrderedList_ConvertsToOl()
    {
        var result = _parser.ToHtml("1. First\n2. Second\n3. Third");

        await Assert.That(result).Contains("<ol");
        await Assert.That(result).Contains("<li>First</li>");
        await Assert.That(result).Contains("<li>Second</li>");
        await Assert.That(result).Contains("<li>Third</li>");
        await Assert.That(result).Contains("</ol>");
    }

    [Test]
    public async Task ToHtml_WithAsteriskList_ConvertsToUl()
    {
        var result = _parser.ToHtml("* Item A\n* Item B");

        await Assert.That(result).Contains("<ul");
        await Assert.That(result).Contains("<li>Item A</li>");
        await Assert.That(result).Contains("<li>Item B</li>");
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Test]
    public async Task ToHtml_WithEmptyString_ReturnsEmptyString()
    {
        await Assert.That(_parser.ToHtml("")).IsEmpty();
    }

    [Test]
    public async Task ToHtml_WithNull_ReturnsEmptyString()
    {
        await Assert.That(_parser.ToHtml(null!)).IsEmpty();
    }

    [Test]
    public async Task ToHtml_WithPlainText_WrapsInParagraph()
    {
        var result = _parser.ToHtml("Just plain text");

        await Assert.That(result).Contains("<p>");
        await Assert.That(result).Contains("Just plain text");
        await Assert.That(result).Contains("</p>");
    }

    [Test]
    public async Task ToHtml_WithDoubleLineBreaks_CreatesParagraphs()
    {
        var result = _parser.ToHtml("Paragraph 1\n\nParagraph 2");

        await Assert.That(result).Contains("Paragraph 1");
        await Assert.That(result).Contains("Paragraph 2");
        // Markdig creates separate <p> tags
        await Assert.That(result).Contains("</p>");
    }

    [Test]
    public async Task ToHtml_WithMixedFormatting_HandlesAllFormats()
    {
        var result = _parser.ToHtml("**Bold** and *italic* with [link](https://example.com) and `code`");

        await Assert.That(result).Contains("<strong>Bold</strong>");
        await Assert.That(result).Contains("<em>italic</em>");
        await Assert.That(result).Contains("<a href=\"https://example.com\"");
        await Assert.That(result).Contains(">link</a>");
        await Assert.That(result).Contains("<code>code</code>");
    }

    [Test]
    public async Task ToHtml_WithSpecialHtmlCharacters_EscapesCharacters()
    {
        var result = _parser.ToHtml("2 < 3 && 4 > 1");

        await Assert.That(result).Contains("&lt;");
        await Assert.That(result).Contains("&gt;");
        await Assert.That(result).Contains("&amp;");
    }

    #endregion

    #region GFM Table Tests

    [Test]
    public async Task ToHtml_WithTable_ConvertsToHtmlTable()
    {
        var result = _parser.ToHtml("| Header 1 | Header 2 |\n|----------|----------|\n| Cell 1   | Cell 2   |");

        await Assert.That(result).Contains("<table");
        await Assert.That(result).Contains("<th>Header 1</th>");
        await Assert.That(result).Contains("<td>Cell 1</td>");
    }

    [Test]
    public async Task ToHtml_WithEntirelyEmptyTable_RemovesTable()
    {
        var result = _parser.ToHtml("| | |\n|---|---|\n| | |");

        await Assert.That(result).DoesNotContain("<table");
    }

    [Test]
    public async Task ToHtml_WithEmptyRows_RemovesEmptyRows()
    {
        var result = _parser.ToHtml("| H1 | H2 |\n|---|---|\n| A | B |\n| | |\n| C | D |");

        await Assert.That(result).Contains("<table");
        await Assert.That(result).Contains("<td>A</td>");
        await Assert.That(result).Contains("<td>C</td>");
        // The empty row should be removed — count <tr> tags (1 header + 2 data = 3)
        var trCount = result.Split("<tr>").Length - 1;
        await Assert.That(trCount).IsEqualTo(3);
    }

    [Test]
    public async Task ToHtml_WithEmptyColumns_RemovesEmptyColumns()
    {
        var result = _parser.ToHtml("| H1 | | H3 |\n|---|---|---|\n| A | | C |\n| D | | F |");

        await Assert.That(result).Contains("<table");
        await Assert.That(result).Contains("<th>H1</th>");
        await Assert.That(result).Contains("<th>H3</th>");
        await Assert.That(result).DoesNotContain("<th></th>");
    }

    [Test]
    public async Task ToHtml_WithContentTable_PreservesTable()
    {
        var result = _parser.ToHtml("| Name | Age |\n|---|---|\n| Alice | 30 |\n| Bob | 25 |");

        await Assert.That(result).Contains("<table");
        await Assert.That(result).Contains("<th>Name</th>");
        await Assert.That(result).Contains("<td>Alice</td>");
        await Assert.That(result).Contains("<td>Bob</td>");
    }

    [Test]
    public async Task ToHtml_WithHeaderOnlyTable_RemovesTable()
    {
        // Header row with content but all data rows empty — should be removed (need at least 2 rows)
        var result = _parser.ToHtml("| H1 | H2 |\n|---|---|\n| | |");

        await Assert.That(result).DoesNotContain("<table");
    }

    [Test]
    public async Task ToHtml_WithHeaderAndOneDataRow_PreservesTable()
    {
        var result = _parser.ToHtml("| H1 | H2 |\n|---|---|\n| A | B |");

        await Assert.That(result).Contains("<table");
        await Assert.That(result).Contains("<td>A</td>");
    }

    #endregion

    #region ToPlainText Tests

    [Test]
    public async Task ToPlainText_RemovesBoldFormatting()
    {
        var result = _parser.ToPlainText("This is **bold** text");

        await Assert.That(result).Contains("This is bold text");
    }

    [Test]
    public async Task ToPlainText_RemovesItalicFormatting()
    {
        var result = _parser.ToPlainText("This is *italic* text");

        await Assert.That(result).Contains("This is italic text");
    }

    [Test]
    public async Task ToPlainText_RemovesCodeMarkers()
    {
        var result = _parser.ToPlainText("Use `code` here");

        await Assert.That(result).Contains("Use code here");
    }

    [Test]
    public async Task ToPlainText_ExtractsLinkText()
    {
        var result = _parser.ToPlainText("Check [this link](https://example.com)");

        await Assert.That(result).Contains("Check this link");
    }

    [Test]
    public async Task ToPlainText_RemovesListMarkers()
    {
        var result = _parser.ToPlainText("- Item 1\n- Item 2");

        await Assert.That(result).Contains("Item 1");
        await Assert.That(result).Contains("Item 2");
    }

    [Test]
    public async Task ToPlainText_RemovesBlockquoteMarkers()
    {
        var result = _parser.ToPlainText("> Quote text");

        await Assert.That(result).Contains("Quote text");
    }

    [Test]
    public async Task ToPlainText_WithNull_ReturnsEmptyString()
    {
        await Assert.That(_parser.ToPlainText(null!)).IsEmpty();
    }

    [Test]
    public async Task ToPlainText_WithEmptyString_ReturnsEmptyString()
    {
        await Assert.That(_parser.ToPlainText("")).IsEmpty();
    }

    [Test]
    public async Task ToPlainText_WithAllFormattingTypes_RemovesAll()
    {
        var result = _parser.ToPlainText("**Bold** *italic* `code` [link](url)\n> quote\n- list");

        await Assert.That(result).Contains("Bold");
        await Assert.That(result).Contains("italic");
        await Assert.That(result).Contains("code");
        await Assert.That(result).Contains("link");
        await Assert.That(result).Contains("quote");
        await Assert.That(result).Contains("list");
        await Assert.That(result).DoesNotContain("**");
        await Assert.That(result).DoesNotContain("`");
        await Assert.That(result).DoesNotContain("[");
        await Assert.That(result).DoesNotContain(">");
    }

    #endregion
}
