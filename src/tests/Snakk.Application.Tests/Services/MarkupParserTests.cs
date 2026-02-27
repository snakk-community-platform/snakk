using Snakk.Application.Services;

namespace Snakk.Application.Tests.Services;

public class MarkupParserTests
{
    private readonly MarkupParser _parser = new();

    #region XSS Prevention Tests (CRITICAL SECURITY)

    [Test]
    public async Task ToHtml_WithScriptTag_EscapesScriptTag()
    {
        // Arrange
        const string maliciousInput = "<script>alert('XSS')</script>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        await Assert.That(result).DoesNotContain("<script>");
        await Assert.That(result).DoesNotContain("</script>");
        await Assert.That(result).Contains("&lt;script&gt;");
        await Assert.That(result).Contains("&lt;/script&gt;");
    }

    [Test]
    public async Task ToHtml_WithOnClickAttribute_EscapesAttribute()
    {
        // Arrange
        const string maliciousInput = "<img src=x onerror=alert('XSS')>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        await Assert.That(result).DoesNotContain("onerror=alert('XSS')");
        await Assert.That(result).Contains("&lt;img");
        await Assert.That(result).Contains("&#39;");
    }

    [Test]
    public async Task ToHtml_WithIframe_EscapesIframe()
    {
        // Arrange
        const string maliciousInput = "<iframe src='javascript:alert(1)'></iframe>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        await Assert.That(result).DoesNotContain("<iframe");
        await Assert.That(result).Contains("&lt;iframe");
    }

    [Test]
    public async Task ToHtml_WithStyleTag_EscapesStyleTag()
    {
        // Arrange
        const string maliciousInput = "<style>body { background: url('javascript:alert(1)') }</style>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        await Assert.That(result).DoesNotContain("<style>");
        await Assert.That(result).Contains("&lt;style&gt;");
    }

    [Test]
    public async Task ToHtml_WithObjectTag_EscapesObjectTag()
    {
        // Arrange
        const string maliciousInput = "<object data='javascript:alert(1)'></object>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        await Assert.That(result).DoesNotContain("<object");
        await Assert.That(result).Contains("&lt;object");
    }

    [Test]
    public async Task ToHtml_WithEmbedTag_EscapesEmbedTag()
    {
        // Arrange
        const string maliciousInput = "<embed src='javascript:alert(1)'>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        await Assert.That(result).DoesNotContain("<embed");
        await Assert.That(result).Contains("&lt;embed");
    }

    [Test]
    [Arguments("<div>test</div>")]
    [Arguments("<span onclick='alert(1)'>test</span>")]
    [Arguments("<a href='javascript:void(0)'>test</a>")]
    [Arguments("<svg onload=alert(1)>")]
    public async Task ToHtml_WithAnyHtmlTag_EscapesTag(string maliciousInput)
    {
        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        await Assert.That(result).Contains("&lt;");
        await Assert.That(result).Contains("&gt;");
    }

    #endregion

    #region URL Validation Tests (CRITICAL SECURITY)

    [Test]
    public async Task ToHtml_WithJavaScriptProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](javascript:alert('XSS'))";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).DoesNotContain("<a href=\"javascript:");
        await Assert.That(result).Contains("[click me]");
    }

    [Test]
    public async Task ToHtml_WithDataProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](data:text/html,<script>alert(1)</script>)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).DoesNotContain("<a href=\"data:");
        await Assert.That(result).Contains("[click me]");
    }

    [Test]
    public async Task ToHtml_WithVbscriptProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](vbscript:msgbox(1))";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).DoesNotContain("<a href=\"vbscript:");
        await Assert.That(result).Contains("[click me]");
    }

    [Test]
    public async Task ToHtml_WithFileProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](file:///etc/passwd)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).DoesNotContain("<a href=\"file:");
        await Assert.That(result).Contains("[click me]");
    }

    [Test]
    public async Task ToHtml_WithHttpUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Google](http://google.com)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<a href=\"http://google.com\"");
        await Assert.That(result).Contains("target=\"_blank\"");
        await Assert.That(result).Contains("rel=\"noopener noreferrer\"");
        await Assert.That(result).Contains(">Google</a>");
    }

    [Test]
    public async Task ToHtml_WithHttpsUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Secure Site](https://example.com)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<a href=\"https://example.com\"");
        await Assert.That(result).Contains(">Secure Site</a>");
    }

    [Test]
    public async Task ToHtml_WithMailtoUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Email me](mailto:test@example.com)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<a href=\"mailto:test@example.com\"");
        await Assert.That(result).Contains(">Email me</a>");
    }

    [Test]
    public async Task ToHtml_WithRelativeUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Internal Link](/some/path)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<a href=\"/some/path\"");
        await Assert.That(result).Contains(">Internal Link</a>");
    }

    [Test]
    public async Task ToHtml_WithProtocolRelativeUrl_DoesNotCreateLink()
    {
        // Arrange - Protocol-relative URLs (//example.com) are not allowed
        const string input = "[Link](//example.com/path)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).DoesNotContain("<a href=\"//example.com");
        await Assert.That(result).Contains("[Link]");
    }

    [Test]
    public async Task ToHtml_WithUrlContainingXss_EscapesUrl()
    {
        // Arrange
        const string input = "[Link](http://example.com/<script>alert(1)</script>)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("&lt;script&gt;");
        await Assert.That(result).DoesNotContain("<script>");
    }

    #endregion

    #region Bold Formatting Tests

    [Test]
    public async Task ToHtml_WithAsteriskBold_ConvertsToBold()
    {
        // Arrange
        const string input = "This is **bold** text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<strong>bold</strong>");
    }

    [Test]
    public async Task ToHtml_WithUnderscoreBold_ConvertsToBold()
    {
        // Arrange
        const string input = "This is __bold__ text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<strong>bold</strong>");
    }

    [Test]
    public async Task ToHtml_WithMultipleBoldSections_ConvertsAll()
    {
        // Arrange
        const string input = "**first** and **second** bold";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<strong>first</strong>");
        await Assert.That(result).Contains("<strong>second</strong>");
    }

    #endregion

    #region Italic Formatting Tests

    [Test]
    public async Task ToHtml_WithAsteriskItalic_ConvertsToItalic()
    {
        // Arrange
        const string input = "This is *italic* text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<em>italic</em>");
    }

    [Test]
    public async Task ToHtml_WithUnderscoreItalic_ConvertsToItalic()
    {
        // Arrange
        const string input = "This is _italic_ text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<em>italic</em>");
    }

    #endregion

    #region Combined Bold and Italic Tests

    [Test]
    public async Task ToHtml_WithBoldAndItalic_ConvertsBoth()
    {
        // Arrange
        const string input = "**bold** and *italic*";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<strong>bold</strong>");
        await Assert.That(result).Contains("<em>italic</em>");
    }

    [Test]
    public async Task ToHtml_WithNestedBoldItalic_HandlesCorrectly()
    {
        // Arrange - In markdown, *** would be bold+italic
        const string input = "***bold and italic***";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        // Should convert ** first (bold), then * (italic)
        await Assert.That(result).Contains("<strong>");
        await Assert.That(result).Contains("</strong>");
    }

    #endregion

    #region Inline Code Tests

    [Test]
    public async Task ToHtml_WithInlineCode_ConvertsToCode()
    {
        // Arrange
        const string input = "Use the `code` function";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<code");
        await Assert.That(result).Contains(">code</code>");
    }

    [Test]
    public async Task ToHtml_WithMultipleInlineCodes_ConvertsAll()
    {
        // Arrange
        const string input = "Use `func1()` or `func2()`";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains(">func1()</code>");
        await Assert.That(result).Contains(">func2()</code>");
    }

    [Test]
    public async Task ToHtml_WithHtmlInInlineCode_EscapesHtml()
    {
        // Arrange
        const string input = "Code: `<script>alert(1)</script>`";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("&lt;script&gt;");
        await Assert.That(result).DoesNotContain("<script>");
    }

    #endregion

    #region Code Block Tests

    [Test]
    public async Task ToHtml_WithCodeBlock_ConvertsToPreCode()
    {
        // Arrange
        const string input = "```\nfunction test() {\n  return 42;\n}\n```";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<pre");
        await Assert.That(result).Contains("<code");
    }

    [Test]
    public async Task ToHtml_WithHtmlInCodeBlock_EscapesHtml()
    {
        // Arrange
        const string input = "```\n<script>alert('XSS')</script>\n```";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("&lt;script&gt;");
        await Assert.That(result).DoesNotContain("<script>");
    }

    [Test]
    public async Task ToHtml_WithCodeBlock_DoesNotApplyFormatting()
    {
        // Arrange
        const string input = "```\n**not bold** and *not italic*\n```";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).DoesNotContain("<strong>");
        await Assert.That(result).DoesNotContain("<em>");
    }

    #endregion

    #region Blockquote Tests

    [Test]
    public async Task ToHtml_WithBlockquote_ConvertsToBlockquote()
    {
        // Arrange
        const string input = "> This is a quote";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<blockquote");
        await Assert.That(result).Contains("This is a quote");
        await Assert.That(result).Contains("</blockquote>");
    }

    [Test]
    public async Task ToHtml_WithMultilineBlockquote_KeepsInSameBlockquote()
    {
        // Arrange
        const string input = "> Line 1\n> Line 2\n> Line 3";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("Line 1");
        await Assert.That(result).Contains("Line 2");
        await Assert.That(result).Contains("Line 3");
        // Should only have one opening and one closing blockquote tag
        await Assert.That(result.Split("<blockquote")).Count().IsEqualTo(2);
        await Assert.That(result.Split("</blockquote>")).Count().IsEqualTo(2);
    }

    #endregion

    #region List Tests

    [Test]
    public async Task ToHtml_WithUnorderedList_ConvertsToUl()
    {
        // Arrange
        const string input = "- Item 1\n- Item 2\n- Item 3";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<ul");
        await Assert.That(result).Contains("<li>Item 1</li>");
        await Assert.That(result).Contains("<li>Item 2</li>");
        await Assert.That(result).Contains("<li>Item 3</li>");
        await Assert.That(result).Contains("</ul>");
    }

    [Test]
    public async Task ToHtml_WithOrderedList_ConvertsToOl()
    {
        // Arrange
        const string input = "1. First\n2. Second\n3. Third";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<ol");
        await Assert.That(result).Contains("<li>First</li>");
        await Assert.That(result).Contains("<li>Second</li>");
        await Assert.That(result).Contains("<li>Third</li>");
        await Assert.That(result).Contains("</ol>");
    }

    [Test]
    public async Task ToHtml_WithAsteriskList_ConvertsToUl()
    {
        // Arrange
        const string input = "* Item A\n* Item B";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<ul");
        await Assert.That(result).Contains("<li>Item A</li>");
        await Assert.That(result).Contains("<li>Item B</li>");
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Test]
    public async Task ToHtml_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        const string input = "";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ToHtml_WithNull_ReturnsEmptyString()
    {
        // Arrange
        string? input = null;

        // Act
        var result = _parser.ToHtml(input!);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ToHtml_WithPlainText_WrapsInParagraph()
    {
        // Arrange
        const string input = "Just plain text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<p");
        await Assert.That(result).Contains("Just plain text");
        await Assert.That(result).Contains("</p>");
    }

    [Test]
    public async Task ToHtml_WithLineBreaks_ConvertsToBr()
    {
        // Arrange
        const string input = "Line 1\nLine 2";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<br>");
    }

    [Test]
    public async Task ToHtml_WithDoubleLineBreaks_CreatesParagraphs()
    {
        // Arrange
        const string input = "Paragraph 1\n\nParagraph 2";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("Paragraph 1");
        await Assert.That(result).Contains("Paragraph 2");
        await Assert.That(result).Contains("</p><p");
    }

    [Test]
    public async Task ToHtml_WithMixedFormatting_HandlesAllFormats()
    {
        // Arrange
        const string input = "**Bold** and *italic* with [link](https://example.com) and `code`";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("<strong>Bold</strong>");
        await Assert.That(result).Contains("<em>italic</em>");
        await Assert.That(result).Contains("<a href=\"https://example.com\"");
        await Assert.That(result).Contains(">link</a>");
        await Assert.That(result).Contains(">code</code>");
    }

    [Test]
    public async Task ToHtml_WithSpecialHtmlCharacters_EscapesCharacters()
    {
        // Arrange
        const string input = "2 < 3 && 4 > 1";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        await Assert.That(result).Contains("&lt;");
        await Assert.That(result).Contains("&gt;");
        await Assert.That(result).Contains("&amp;");
    }

    #endregion

    #region ToPlainText Tests

    [Test]
    public async Task ToPlainText_RemovesBoldFormatting()
    {
        // Arrange
        const string input = "This is **bold** text";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).IsEqualTo("This is bold text");
    }

    [Test]
    public async Task ToPlainText_RemovesItalicFormatting()
    {
        // Arrange
        const string input = "This is *italic* text";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).IsEqualTo("This is italic text");
    }

    [Test]
    public async Task ToPlainText_RemovesCodeMarkers()
    {
        // Arrange
        const string input = "Use `code` here";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).IsEqualTo("Use code here");
    }

    [Test]
    public async Task ToPlainText_ExtractsLinkText()
    {
        // Arrange
        const string input = "Check [this link](https://example.com)";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).IsEqualTo("Check this link");
    }

    [Test]
    public async Task ToPlainText_RemovesListMarkers()
    {
        // Arrange
        const string input = "- Item 1\n- Item 2";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).Contains("Item 1");
        await Assert.That(result).Contains("Item 2");
        await Assert.That(result).DoesNotContain("-");
    }

    [Test]
    public async Task ToPlainText_RemovesBlockquoteMarkers()
    {
        // Arrange
        const string input = "> Quote text";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).IsEqualTo("Quote text");
    }

    [Test]
    public async Task ToPlainText_WithNull_ReturnsEmptyString()
    {
        // Arrange
        string? input = null;

        // Act
        var result = _parser.ToPlainText(input!);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ToPlainText_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        const string input = "";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ToPlainText_WithAllFormattingTypes_RemovesAll()
    {
        // Arrange
        const string input = "**Bold** *italic* `code` [link](url)\n> quote\n- list";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        await Assert.That(result).Contains("Bold");
        await Assert.That(result).Contains("italic");
        await Assert.That(result).Contains("code");
        await Assert.That(result).Contains("link");
        await Assert.That(result).Contains("quote");
        await Assert.That(result).Contains("list");
        await Assert.That(result).DoesNotContain("**");
        await Assert.That(result).DoesNotContain("*");
        await Assert.That(result).DoesNotContain("`");
        await Assert.That(result).DoesNotContain("[");
        await Assert.That(result).DoesNotContain(">");
        await Assert.That(result).DoesNotContain("-");
    }

    #endregion
}
