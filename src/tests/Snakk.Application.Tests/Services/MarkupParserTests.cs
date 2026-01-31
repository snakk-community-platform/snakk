using FluentAssertions;
using Snakk.Application.Services;
using Xunit;

namespace Snakk.Application.Tests.Services;

public class MarkupParserTests
{
    private readonly MarkupParser _parser = new();

    #region XSS Prevention Tests (CRITICAL SECURITY)

    [Fact]
    public void ToHtml_WithScriptTag_EscapesScriptTag()
    {
        // Arrange
        const string maliciousInput = "<script>alert('XSS')</script>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        result.Should().NotContain("<script>");
        result.Should().NotContain("</script>");
        result.Should().Contain("&lt;script&gt;");
        result.Should().Contain("&lt;/script&gt;");
    }

    [Fact]
    public void ToHtml_WithOnClickAttribute_EscapesAttribute()
    {
        // Arrange
        const string maliciousInput = "<img src=x onerror=alert('XSS')>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        result.Should().NotContain("onerror=alert('XSS')"); // Should be HTML encoded, not executable
        result.Should().Contain("&lt;img");
        result.Should().Contain("&#39;"); // Single quotes should be encoded
    }

    [Fact]
    public void ToHtml_WithIframe_EscapesIframe()
    {
        // Arrange
        const string maliciousInput = "<iframe src='javascript:alert(1)'></iframe>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        result.Should().NotContain("<iframe");
        result.Should().Contain("&lt;iframe");
    }

    [Fact]
    public void ToHtml_WithStyleTag_EscapesStyleTag()
    {
        // Arrange
        const string maliciousInput = "<style>body { background: url('javascript:alert(1)') }</style>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        result.Should().NotContain("<style>");
        result.Should().Contain("&lt;style&gt;");
    }

    [Fact]
    public void ToHtml_WithObjectTag_EscapesObjectTag()
    {
        // Arrange
        const string maliciousInput = "<object data='javascript:alert(1)'></object>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        result.Should().NotContain("<object");
        result.Should().Contain("&lt;object");
    }

    [Fact]
    public void ToHtml_WithEmbedTag_EscapesEmbedTag()
    {
        // Arrange
        const string maliciousInput = "<embed src='javascript:alert(1)'>";

        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        result.Should().NotContain("<embed");
        result.Should().Contain("&lt;embed");
    }

    [Theory]
    [InlineData("<div>test</div>")]
    [InlineData("<span onclick='alert(1)'>test</span>")]
    [InlineData("<a href='javascript:void(0)'>test</a>")]
    [InlineData("<svg onload=alert(1)>")]
    public void ToHtml_WithAnyHtmlTag_EscapesTag(string maliciousInput)
    {
        // Act
        var result = _parser.ToHtml(maliciousInput);

        // Assert
        result.Should().Contain("&lt;");
        result.Should().Contain("&gt;");
    }

    #endregion

    #region URL Validation Tests (CRITICAL SECURITY)

    [Fact]
    public void ToHtml_WithJavaScriptProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](javascript:alert('XSS'))";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().NotContain("<a href=\"javascript:");
        result.Should().Contain("[click me]");
    }

    [Fact]
    public void ToHtml_WithDataProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](data:text/html,<script>alert(1)</script>)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().NotContain("<a href=\"data:");
        result.Should().Contain("[click me]");
    }

    [Fact]
    public void ToHtml_WithVbscriptProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](vbscript:msgbox(1))";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().NotContain("<a href=\"vbscript:");
        result.Should().Contain("[click me]");
    }

    [Fact]
    public void ToHtml_WithFileProtocol_DoesNotCreateLink()
    {
        // Arrange
        const string input = "[click me](file:///etc/passwd)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().NotContain("<a href=\"file:");
        result.Should().Contain("[click me]");
    }

    [Fact]
    public void ToHtml_WithHttpUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Google](http://google.com)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<a href=\"http://google.com\"");
        result.Should().Contain("target=\"_blank\"");
        result.Should().Contain("rel=\"noopener noreferrer\"");
        result.Should().Contain(">Google</a>");
    }

    [Fact]
    public void ToHtml_WithHttpsUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Secure Site](https://example.com)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<a href=\"https://example.com\"");
        result.Should().Contain(">Secure Site</a>");
    }

    [Fact]
    public void ToHtml_WithMailtoUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Email me](mailto:test@example.com)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<a href=\"mailto:test@example.com\"");
        result.Should().Contain(">Email me</a>");
    }

    [Fact]
    public void ToHtml_WithRelativeUrl_CreatesLink()
    {
        // Arrange
        const string input = "[Internal Link](/some/path)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<a href=\"/some/path\"");
        result.Should().Contain(">Internal Link</a>");
    }

    [Fact]
    public void ToHtml_WithProtocolRelativeUrl_DoesNotCreateLink()
    {
        // Arrange - Protocol-relative URLs (//example.com) are not allowed
        const string input = "[Link](//example.com/path)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().NotContain("<a href=\"//example.com");
        result.Should().Contain("[Link]");
    }

    [Fact]
    public void ToHtml_WithUrlContainingXss_EscapesUrl()
    {
        // Arrange
        const string input = "[Link](http://example.com/<script>alert(1)</script>)";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("&lt;script&gt;");
        result.Should().NotContain("<script>");
    }

    #endregion

    #region Bold Formatting Tests

    [Fact]
    public void ToHtml_WithAsteriskBold_ConvertsToBold()
    {
        // Arrange
        const string input = "This is **bold** text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<strong>bold</strong>");
    }

    [Fact]
    public void ToHtml_WithUnderscoreBold_ConvertsToBold()
    {
        // Arrange
        const string input = "This is __bold__ text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<strong>bold</strong>");
    }

    [Fact]
    public void ToHtml_WithMultipleBoldSections_ConvertsAll()
    {
        // Arrange
        const string input = "**first** and **second** bold";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<strong>first</strong>");
        result.Should().Contain("<strong>second</strong>");
    }

    #endregion

    #region Italic Formatting Tests

    [Fact]
    public void ToHtml_WithAsteriskItalic_ConvertsToItalic()
    {
        // Arrange
        const string input = "This is *italic* text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<em>italic</em>");
    }

    [Fact]
    public void ToHtml_WithUnderscoreItalic_ConvertsToItalic()
    {
        // Arrange
        const string input = "This is _italic_ text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<em>italic</em>");
    }

    #endregion

    #region Combined Bold and Italic Tests

    [Fact]
    public void ToHtml_WithBoldAndItalic_ConvertsBoth()
    {
        // Arrange
        const string input = "**bold** and *italic*";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<strong>bold</strong>");
        result.Should().Contain("<em>italic</em>");
    }

    [Fact]
    public void ToHtml_WithNestedBoldItalic_HandlesCorrectly()
    {
        // Arrange - In markdown, *** would be bold+italic
        const string input = "***bold and italic***";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        // Should convert ** first (bold), then * (italic)
        result.Should().Contain("<strong>");
        result.Should().Contain("</strong>");
    }

    #endregion

    #region Inline Code Tests

    [Fact]
    public void ToHtml_WithInlineCode_ConvertsToCode()
    {
        // Arrange
        const string input = "Use the `code` function";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<code");
        result.Should().Contain(">code</code>");
    }

    [Fact]
    public void ToHtml_WithMultipleInlineCodes_ConvertsAll()
    {
        // Arrange
        const string input = "Use `func1()` or `func2()`";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain(">func1()</code>");
        result.Should().Contain(">func2()</code>");
    }

    [Fact]
    public void ToHtml_WithHtmlInInlineCode_EscapesHtml()
    {
        // Arrange
        const string input = "Code: `<script>alert(1)</script>`";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("&lt;script&gt;");
        result.Should().NotContain("<script>");
    }

    #endregion

    #region Code Block Tests

    [Fact]
    public void ToHtml_WithCodeBlock_ConvertsToPreCode()
    {
        // Arrange
        const string input = "```\nfunction test() {\n  return 42;\n}\n```";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<pre");
        result.Should().Contain("<code");
    }

    [Fact]
    public void ToHtml_WithHtmlInCodeBlock_EscapesHtml()
    {
        // Arrange
        const string input = "```\n<script>alert('XSS')</script>\n```";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("&lt;script&gt;");
        result.Should().NotContain("<script>");
    }

    [Fact]
    public void ToHtml_WithCodeBlock_DoesNotApplyFormatting()
    {
        // Arrange
        const string input = "```\n**not bold** and *not italic*\n```";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().NotContain("<strong>");
        result.Should().NotContain("<em>");
    }

    #endregion

    #region Blockquote Tests

    [Fact]
    public void ToHtml_WithBlockquote_ConvertsToBlockquote()
    {
        // Arrange
        const string input = "> This is a quote";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<blockquote");
        result.Should().Contain("This is a quote");
        result.Should().Contain("</blockquote>");
    }

    [Fact]
    public void ToHtml_WithMultilineBlockquote_KeepsInSameBlockquote()
    {
        // Arrange
        const string input = "> Line 1\n> Line 2\n> Line 3";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("Line 1");
        result.Should().Contain("Line 2");
        result.Should().Contain("Line 3");
        // Should only have one opening and one closing blockquote tag
        result.Split("<blockquote").Should().HaveCount(2); // Original + 1 split
        result.Split("</blockquote>").Should().HaveCount(2);
    }

    #endregion

    #region List Tests

    [Fact]
    public void ToHtml_WithUnorderedList_ConvertsToUl()
    {
        // Arrange
        const string input = "- Item 1\n- Item 2\n- Item 3";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<ul");
        result.Should().Contain("<li>Item 1</li>");
        result.Should().Contain("<li>Item 2</li>");
        result.Should().Contain("<li>Item 3</li>");
        result.Should().Contain("</ul>");
    }

    [Fact]
    public void ToHtml_WithOrderedList_ConvertsToOl()
    {
        // Arrange
        const string input = "1. First\n2. Second\n3. Third";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<ol");
        result.Should().Contain("<li>First</li>");
        result.Should().Contain("<li>Second</li>");
        result.Should().Contain("<li>Third</li>");
        result.Should().Contain("</ol>");
    }

    [Fact]
    public void ToHtml_WithAsteriskList_ConvertsToUl()
    {
        // Arrange
        const string input = "* Item A\n* Item B";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<ul");
        result.Should().Contain("<li>Item A</li>");
        result.Should().Contain("<li>Item B</li>");
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Fact]
    public void ToHtml_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        const string input = "";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToHtml_WithNull_ReturnsEmptyString()
    {
        // Arrange
        string? input = null;

        // Act
        var result = _parser.ToHtml(input!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToHtml_WithPlainText_WrapsInParagraph()
    {
        // Arrange
        const string input = "Just plain text";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<p");
        result.Should().Contain("Just plain text");
        result.Should().Contain("</p>");
    }

    [Fact]
    public void ToHtml_WithLineBreaks_ConvertsToBr()
    {
        // Arrange
        const string input = "Line 1\nLine 2";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<br>");
    }

    [Fact]
    public void ToHtml_WithDoubleLineBreaks_CreatesParagraphs()
    {
        // Arrange
        const string input = "Paragraph 1\n\nParagraph 2";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("Paragraph 1");
        result.Should().Contain("Paragraph 2");
        result.Should().Contain("</p><p");
    }

    [Fact]
    public void ToHtml_WithMixedFormatting_HandlesAllFormats()
    {
        // Arrange
        const string input = "**Bold** and *italic* with [link](https://example.com) and `code`";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("<strong>Bold</strong>");
        result.Should().Contain("<em>italic</em>");
        result.Should().Contain("<a href=\"https://example.com\"");
        result.Should().Contain(">link</a>");
        result.Should().Contain(">code</code>");
    }

    [Fact]
    public void ToHtml_WithSpecialHtmlCharacters_EscapesCharacters()
    {
        // Arrange
        const string input = "2 < 3 && 4 > 1";

        // Act
        var result = _parser.ToHtml(input);

        // Assert
        result.Should().Contain("&lt;");
        result.Should().Contain("&gt;");
        result.Should().Contain("&amp;");
    }

    #endregion

    #region ToPlainText Tests

    [Fact]
    public void ToPlainText_RemovesBoldFormatting()
    {
        // Arrange
        const string input = "This is **bold** text";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().Be("This is bold text");
    }

    [Fact]
    public void ToPlainText_RemovesItalicFormatting()
    {
        // Arrange
        const string input = "This is *italic* text";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().Be("This is italic text");
    }

    [Fact]
    public void ToPlainText_RemovesCodeMarkers()
    {
        // Arrange
        const string input = "Use `code` here";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().Be("Use code here");
    }

    [Fact]
    public void ToPlainText_ExtractsLinkText()
    {
        // Arrange
        const string input = "Check [this link](https://example.com)";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().Be("Check this link");
    }

    [Fact]
    public void ToPlainText_RemovesListMarkers()
    {
        // Arrange
        const string input = "- Item 1\n- Item 2";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().Contain("Item 1");
        result.Should().Contain("Item 2");
        result.Should().NotContain("-");
    }

    [Fact]
    public void ToPlainText_RemovesBlockquoteMarkers()
    {
        // Arrange
        const string input = "> Quote text";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().Be("Quote text");
    }

    [Fact]
    public void ToPlainText_WithNull_ReturnsEmptyString()
    {
        // Arrange
        string? input = null;

        // Act
        var result = _parser.ToPlainText(input!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToPlainText_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        const string input = "";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToPlainText_WithAllFormattingTypes_RemovesAll()
    {
        // Arrange
        const string input = "**Bold** *italic* `code` [link](url)\n> quote\n- list";

        // Act
        var result = _parser.ToPlainText(input);

        // Assert
        result.Should().Contain("Bold");
        result.Should().Contain("italic");
        result.Should().Contain("code");
        result.Should().Contain("link");
        result.Should().Contain("quote");
        result.Should().Contain("list");
        result.Should().NotContain("**");
        result.Should().NotContain("*");
        result.Should().NotContain("`");
        result.Should().NotContain("[");
        result.Should().NotContain(">");
        result.Should().NotContain("-");
    }

    #endregion
}
